#!/bin/bash

# ReFrontier Parallelism Benchmark Script
# Measures performance with different parallelism levels on real data

echo "═══════════════════════════════════════════════════════════"
echo "    ReFrontier Parallelism Performance Benchmark"
echo "═══════════════════════════════════════════════════════════"
echo ""

# Configuration
BACKUP_DIR="${1:-backup/dat}"
OUTPUT_DIR="/tmp/refrontier_bench_$$"
NUM_FILES="${2:-50}"

# Build the project
echo "Building ReFrontier..."
dotnet build ReFrontier/ReFrontier.csproj --configuration Release > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "Build failed, trying debug build..."
    dotnet build ReFrontier/ReFrontier.csproj --configuration Debug > /dev/null 2>&1
    if [ $? -ne 0 ]; then
        echo "Build failed!"
        exit 1
    fi
    BUILD_CONFIG="Debug"
else
    BUILD_CONFIG="Release"
fi
echo "Using $BUILD_CONFIG build"
echo ""

# Find test files
echo "Finding test files in $BACKUP_DIR..."
TEST_FILES=$(find "$BACKUP_DIR" -name "*.bin" -type f ! -name "*.decd.bin" ! -path "*.unpacked/*" | head -n "$NUM_FILES")
FILE_COUNT=$(echo "$TEST_FILES" | wc -l)

if [ "$FILE_COUNT" -eq 0 ]; then
    echo "No .bin files found in $BACKUP_DIR"
    exit 1
fi

echo "Selected $FILE_COUNT files for benchmarking"

# Calculate total size
TOTAL_SIZE=$(echo "$TEST_FILES" | xargs du -b | awk '{s+=$1} END {print s}')
TOTAL_MB=$(echo "scale=2; $TOTAL_SIZE / 1024 / 1024" | bc)
echo "Total size: ${TOTAL_MB} MB"
echo "System has $(nproc) processor cores"
echo ""

# Test different parallelism levels
PARALLELISM_LEVELS="1 2 4 8 $(nproc)"

echo "Running benchmarks..."
echo ""

declare -a TIMES
declare -a LEVELS

for PARALLEL in $PARALLELISM_LEVELS; do
    # Clean output directory
    rm -rf "$OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"

    echo "Testing parallelism = $PARALLEL..."

    # Create a temporary directory for this test batch
    TEST_BATCH_DIR="$OUTPUT_DIR/test_batch_$PARALLEL"
    mkdir -p "$TEST_BATCH_DIR"

    # Copy files to test batch (to avoid conflicts)
    i=1
    while IFS= read -r file; do
        if [ -f "$file" ]; then
            cp "$file" "$TEST_BATCH_DIR/testfile_$(printf "%04d" $i).bin" 2>/dev/null
            i=$((i + 1))
        fi
    done <<< "$TEST_FILES"

    # Run the benchmark
    START=$(date +%s%3N)

    find "$TEST_BATCH_DIR" -name "*.bin" -type f | while read -r file; do
        ./ReFrontier/bin/$BUILD_CONFIG/net8.0/ReFrontier "$file" --parallelism "$PARALLEL" --nonRecursive > /dev/null 2>&1
    done

    END=$(date +%s%3N)
    TIME_MS=$((END - START))

    TIMES+=($TIME_MS)
    LEVELS+=($PARALLEL)

    # Calculate metrics
    THROUGHPUT=$(echo "scale=2; $FILE_COUNT * 1000 / $TIME_MS" | bc)
    MB_PER_SEC=$(echo "scale=2; $TOTAL_MB * 1000 / $TIME_MS" | bc)

    echo "  Time: ${TIME_MS} ms | Throughput: ${THROUGHPUT} files/s | Speed: ${MB_PER_SEC} MB/s"
    echo ""
done

# Print summary table
echo ""
echo "═══════════════════════════════════════════════════════════════════════════"
echo "                         BENCHMARK RESULTS"
echo "═══════════════════════════════════════════════════════════════════════════"
echo "Parallelism │   Time (ms) │ Files/sec │  MB/sec │ Speedup vs Sequential"
echo "────────────┼─────────────┼───────────┼─────────┼──────────────────────"

BASELINE_TIME=${TIMES[0]}

for i in "${!TIMES[@]}"; do
    TIME_MS=${TIMES[$i]}
    PARALLEL=${LEVELS[$i]}

    THROUGHPUT=$(echo "scale=2; $FILE_COUNT * 1000 / $TIME_MS" | bc)
    MB_PER_SEC=$(echo "scale=2; $TOTAL_MB * 1000 / $TIME_MS" | bc)
    SPEEDUP=$(echo "scale=2; $BASELINE_TIME / $TIME_MS" | bc)

    if [ "$PARALLEL" -eq "$(nproc)" ]; then
        PARALLEL_STR="$PARALLEL (auto)"
    else
        PARALLEL_STR="$PARALLEL"
    fi

    printf "%11s │ %11d │ %9.2f │ %7.2f │ %20.2fx\n" "$PARALLEL_STR" "$TIME_MS" "$THROUGHPUT" "$MB_PER_SEC" "$SPEEDUP"
done

echo "═══════════════════════════════════════════════════════════════════════════"

# Calculate efficiency
echo ""
echo "Parallel Efficiency Analysis:"
for i in "${!TIMES[@]}"; do
    if [ $i -eq 0 ]; then
        continue  # Skip sequential baseline
    fi

    TIME_MS=${TIMES[$i]}
    PARALLEL=${LEVELS[$i]}
    SPEEDUP=$(echo "scale=4; $BASELINE_TIME / $TIME_MS" | bc)
    EFFICIENCY=$(echo "scale=1; ($SPEEDUP / $PARALLEL) * 100" | bc)

    printf "  %2d threads: %5.1f%% efficient (ideal = 100%%)\n" "$PARALLEL" "$EFFICIENCY"
done

# Clean up
rm -rf "$OUTPUT_DIR"

echo ""
echo "Benchmark complete!"
