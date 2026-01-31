#!/bin/bash

# ReFrontier Real-World Parallelism Benchmark
# Tests full decrypt + decompress + unpack pipeline

echo "═══════════════════════════════════════════════════════════"
echo "   ReFrontier Real-World Parallelism Benchmark"
echo "   Full Decrypt → Decompress → Unpack Pipeline"
echo "═══════════════════════════════════════════════════════════"
echo ""

# Configuration
BACKUP_DIR="${1:-backup/dat}"
NUM_FILES="${2:-20}"
TEMP_BASE="/tmp/refrontier_real_bench_$$"

# Build
echo "Building ReFrontier..."
dotnet build ReFrontier/ReFrontier.csproj --configuration Release > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi
echo ""

# Find large encrypted/compressed files (likely ECD or JKR)
echo "Finding compressed/encrypted files in $BACKUP_DIR..."
LARGE_FILES=$(find "$BACKUP_DIR" -name "*.bin" -type f -size +100k ! -name "*.decd*" ! -path "*.unpacked/*" | head -n "$NUM_FILES")
FILE_COUNT=$(echo "$LARGE_FILES" | grep -c .)

if [ "$FILE_COUNT" -eq 0 ]; then
    echo "No suitable files found!"
    exit 1
fi

echo "Selected $FILE_COUNT files for benchmarking"

# Calculate total size
TOTAL_SIZE=0
while IFS= read -r file; do
    SIZE=$(stat -f%z "$file" 2>/dev/null || stat -c%s "$file" 2>/dev/null || echo 0)
    TOTAL_SIZE=$((TOTAL_SIZE + SIZE))
done <<< "$LARGE_FILES"

TOTAL_MB=$(echo "scale=2; $TOTAL_SIZE / 1024 / 1024" | bc)
echo "Total input size: ${TOTAL_MB} MB"
echo "System has $(nproc) processor cores"
echo ""

# Test parallelism levels
PARALLELISM_LEVELS="1 2 4 8 $(nproc)"

echo "Running full processing benchmarks (decrypt + decompress + unpack)..."
echo ""

declare -a TIMES
declare -a LEVELS
declare -a OUTPUT_SIZES

for PARALLEL in $PARALLELISM_LEVELS; do
    echo "Testing parallelism = $PARALLEL..."

    # Create fresh test directory
    TEST_DIR="$TEMP_BASE/test_$PARALLEL"
    mkdir -p "$TEST_DIR"

    # Copy original files to test directory
    cp_count=0
    while IFS= read -r file; do
        if [ -f "$file" ]; then
            cp "$file" "$TEST_DIR/" 2>/dev/null
            cp_count=$((cp_count + 1))
        fi
    done <<< "$LARGE_FILES"

    # Change to test directory to process files there
    cd "$TEST_DIR" || exit 1

    # Run benchmark - process entire directory at once for true parallelism
    START=$(date +%s%N)

    /home/h/Documents/dev/ho/Mogapédia/MHFrontier/ReFrontier/ReFrontier/bin/Release/net8.0/ReFrontier \
        "." \
        --parallelism "$PARALLEL" \
        --noFileRewrite \
        > /dev/null 2>&1

    END=$(date +%s%N)
    TIME_NS=$((END - START))
    TIME_MS=$((TIME_NS / 1000000))

    # Calculate output size (decompressed data)
    OUTPUT_SIZE=$(du -sb . 2>/dev/null | awk '{print $1}' || echo "$TOTAL_SIZE")
    OUTPUT_MB=$(echo "scale=2; $OUTPUT_SIZE / 1024 / 1024" | bc)

    TIMES+=($TIME_MS)
    LEVELS+=($PARALLEL)
    OUTPUT_SIZES+=($OUTPUT_SIZE)

    # Calculate metrics
    if [ $TIME_MS -gt 0 ]; then
        THROUGHPUT=$(echo "scale=2; $FILE_COUNT * 1000 / $TIME_MS" | bc)
        INPUT_MB_PER_SEC=$(echo "scale=2; $TOTAL_MB * 1000 / $TIME_MS" | bc)
        OUTPUT_MB_PER_SEC=$(echo "scale=2; $OUTPUT_MB * 1000 / $TIME_MS" | bc)
    else
        THROUGHPUT="N/A"
        INPUT_MB_PER_SEC="N/A"
        OUTPUT_MB_PER_SEC="N/A"
    fi

    echo "  Time: ${TIME_MS} ms"
    echo "  Input: ${THROUGHPUT} files/s, ${INPUT_MB_PER_SEC} MB/s"
    echo "  Output: ${OUTPUT_MB} MB decompressed, ${OUTPUT_MB_PER_SEC} MB/s write"
    echo ""

    # Return to original directory and clean up
    cd - > /dev/null
    rm -rf "$TEST_DIR"
done

# Clean up base temp directory
rm -rf "$TEMP_BASE"

# Print summary
echo ""
echo "═══════════════════════════════════════════════════════════════════════════════════"
echo "                              BENCHMARK RESULTS"
echo "                   (Full Decrypt + Decompress + Unpack Pipeline)"
echo "═══════════════════════════════════════════════════════════════════════════════════"
echo "Parallelism │   Time (ms) │ Files/s │ Input MB/s │ Output MB/s │ Speedup"
echo "────────────┼─────────────┼─────────┼────────────┼─────────────┼────────"

BASELINE_TIME=${TIMES[0]}

for i in "${!TIMES[@]}"; do
    TIME_MS=${TIMES[$i]}
    PARALLEL=${LEVELS[$i]}

    if [ $TIME_MS -gt 0 ]; then
        THROUGHPUT=$(echo "scale=2; $FILE_COUNT * 1000 / $TIME_MS" | bc)
        INPUT_MBPS=$(echo "scale=2; $TOTAL_MB * 1000 / $TIME_MS" | bc)
        OUTPUT_MB=$(echo "scale=2; ${OUTPUT_SIZES[$i]} / 1024 / 1024" | bc)
        OUTPUT_MBPS=$(echo "scale=2; $OUTPUT_MB * 1000 / $TIME_MS" | bc)
        SPEEDUP=$(echo "scale=2; $BASELINE_TIME / $TIME_MS" | bc)
    else
        THROUGHPUT="0.00"
        INPUT_MBPS="0.00"
        OUTPUT_MBPS="0.00"
        SPEEDUP="0.00"
    fi

    if [ "$PARALLEL" -eq "$(nproc)" ]; then
        PARALLEL_STR="$PARALLEL (auto)"
    else
        PARALLEL_STR="$PARALLEL"
    fi

    printf "%11s │ %11d │ %7s │ %10s │ %11s │ %7sx\n" \
        "$PARALLEL_STR" "$TIME_MS" "$THROUGHPUT" "$INPUT_MBPS" "$OUTPUT_MBPS" "$SPEEDUP"
done

echo "═══════════════════════════════════════════════════════════════════════════════════"

# Efficiency analysis
echo ""
echo "Parallel Efficiency:"
for i in "${!TIMES[@]}"; do
    if [ $i -eq 0 ]; then
        continue
    fi

    TIME_MS=${TIMES[$i]}
    PARALLEL=${LEVELS[$i]}

    if [ $TIME_MS -gt 0 ] && [ $BASELINE_TIME -gt 0 ]; then
        SPEEDUP=$(echo "scale=4; $BASELINE_TIME / $TIME_MS" | bc)
        EFFICIENCY=$(echo "scale=1; ($SPEEDUP / $PARALLEL) * 100" | bc)
        printf "  %2d threads: %6.2fx speedup, %5.1f%% efficient\n" "$PARALLEL" "$SPEEDUP" "$EFFICIENCY"
    fi
done

echo ""
echo "Compression ratio: $(echo "scale=2; ${OUTPUT_SIZES[0]} / $TOTAL_SIZE" | bc)x"
echo "Benchmark complete!"
