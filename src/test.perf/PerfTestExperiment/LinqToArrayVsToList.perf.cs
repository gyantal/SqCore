using BenchmarkDotNet.Attributes;

using System.Linq;
using System.Collections.Generic;

namespace SqBenchmarks
{

    [MemoryDiagnoser]
    public class BnchLinqToArrayVsToList
    {
        [Params(0, 1, 6, 10, 39, 100, 666, 1000, 1337, 10000)]
        public int Count { get; set; }

        public IEnumerable<int> Items;

        [GlobalSetup]
        public void GlobalSetup()   // executed once per each Params value
        {
        // Version 1: If Length is known: ToList() takes 2.5x-3x more time than ToArray(). (+150-200%)
        Items = Enumerable.Range(0, Count);
// BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1348 (21H1/May2021Update)
// Intel Core i9-9900K CPU 3.60GHz (Coffee Lake), 1 CPU, 16 logical and 8 physical cores, .NET SDK=5.0.103
// |    Method | Count |          Mean |      Error |     StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
// |---------- |------ |--------------:|-----------:|-----------:|------:|--------:|-------:|-------:|----------:|
// | ToArray() |     0 |      2.475 ns |  0.0229 ns |  0.0203 ns |  1.00 |    0.00 |      - |      - |         - |
// |  ToList() |     0 |      5.974 ns |  0.0520 ns |  0.0486 ns |  2.41 |    0.04 | 0.0038 |      - |      32 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |     1 |      6.391 ns |  0.0657 ns |  0.0582 ns |  1.00 |    0.00 | 0.0038 |      - |      32 B |
// |  ToList() |     1 |     10.718 ns |  0.1012 ns |  0.0946 ns |  1.68 |    0.02 | 0.0076 |      - |      64 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |     6 |      8.127 ns |  0.0739 ns |  0.0691 ns |  1.00 |    0.00 | 0.0057 |      - |      48 B |
// |  ToList() |     6 |     17.325 ns |  0.1595 ns |  0.1492 ns |  2.13 |    0.02 | 0.0095 |      - |      80 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |    10 |     10.735 ns |  0.1063 ns |  0.0887 ns |  1.00 |    0.00 | 0.0076 |      - |      64 B |
// |  ToList() |    10 |     23.564 ns |  0.1791 ns |  0.1676 ns |  2.20 |    0.03 | 0.0115 |      - |      96 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |    39 |     24.599 ns |  0.3703 ns |  0.3464 ns |  1.00 |    0.00 | 0.0220 |      - |     184 B |
// |  ToList() |    39 |     67.743 ns |  0.5298 ns |  0.4956 ns |  2.75 |    0.04 | 0.0257 |      - |     216 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |   100 |     59.399 ns |  1.2008 ns |  1.4747 ns |  1.00 |    0.00 | 0.0507 |      - |     424 B |
// |  ToList() |   100 |    139.945 ns |  1.5549 ns |  1.3784 ns |  2.38 |    0.05 | 0.0544 |      - |     456 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |   666 |    309.128 ns |  3.9423 ns |  3.4948 ns |  1.00 |    0.00 | 0.3209 | 0.0029 |   2,688 B |
// |  ToList() |   666 |    808.731 ns |  7.0055 ns |  6.2102 ns |  2.62 |    0.02 | 0.3242 | 0.0029 |   2,720 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |  1000 |    459.217 ns |  6.1807 ns |  5.4790 ns |  1.00 |    0.00 | 0.4807 | 0.0072 |   4,024 B |
// |  ToList() |  1000 |  1,199.417 ns |  6.4922 ns |  6.0728 ns |  2.61 |    0.04 | 0.4845 | 0.0134 |   4,056 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() |  1337 |    602.967 ns | 11.4125 ns | 10.6753 ns |  1.00 |    0.00 | 0.6418 | 0.0124 |   5,376 B |
// |  ToList() |  1337 |  1,609.828 ns | 10.3847 ns |  9.7139 ns |  2.67 |    0.06 | 0.6447 | 0.0248 |   5,408 B |
// |           |       |               |            |            |       |         |        |        |           |
// | ToArray() | 10000 |  4,320.787 ns | 58.9595 ns | 52.2661 ns |  1.00 |    0.00 | 4.7607 | 0.5951 |  40,024 B |
// |  ToList() | 10000 | 11,787.057 ns | 63.3952 ns | 56.1982 ns |  2.73 |    0.03 | 4.7607 | 0.9460 |  40,056 B |

        // Version 2: If Length is unknown (typical with Where()): ToList() is a 1-4% faster.
        // Items = Enumerable.Range(0, Count).Where(i => i % 2 == 0);
// BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1348 (21H1/May2021Update)
// Intel Core i9-9900K CPU 3.60GHz (Coffee Lake), 1 CPU, 16 logical and 8 physical cores, .NET SDK=5.0.103
// |    Method | Count |         Mean |      Error |     StdDev | Ratio |  Gen 0 |  Gen 1 | Allocated |
// |---------- |------ |-------------:|-----------:|-----------:|------:|-------:|-------:|----------:|
// | ToArray() |     0 |     14.94 ns |   0.069 ns |   0.057 ns |  1.00 |      - |      - |         - |
// |  ToList() |     0 |     10.87 ns |   0.114 ns |   0.107 ns |  0.73 | 0.0038 |      - |      32 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |     1 |     51.38 ns |   0.136 ns |   0.114 ns |  1.00 | 0.0134 |      - |     112 B |
// |  ToList() |     1 |     33.28 ns |   0.329 ns |   0.274 ns |  0.65 | 0.0134 |      - |     112 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |     6 |     76.37 ns |   0.685 ns |   0.640 ns |  1.00 | 0.0143 |      - |     120 B |
// |  ToList() |     6 |     58.69 ns |   0.260 ns |   0.217 ns |  0.77 | 0.0134 |      - |     112 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |    10 |    112.47 ns |   1.803 ns |   1.686 ns |  1.00 | 0.0219 |      - |     184 B |
// |  ToList() |    10 |     93.36 ns |   0.463 ns |   0.411 ns |  0.83 | 0.0200 |      - |     168 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |    39 |    340.12 ns |   2.154 ns |   2.015 ns |  1.00 | 0.0525 |      - |     440 B |
// |  ToList() |    39 |    306.25 ns |   2.111 ns |   1.763 ns |  0.90 | 0.0486 |      - |     408 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |   100 |    714.29 ns |   7.158 ns |   6.696 ns |  1.00 | 0.0849 |      - |     712 B |
// |  ToList() |   100 |    659.11 ns |   3.455 ns |   3.232 ns |  0.92 | 0.0820 |      - |     688 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |   666 |  3,885.53 ns |  27.718 ns |  24.571 ns |  1.00 | 0.4501 |      - |   3,800 B |
// |  ToList() |   666 |  3,742.60 ns |  25.970 ns |  24.293 ns |  0.96 | 0.5188 | 0.0076 |   4,344 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |  1000 |  5,667.76 ns |  46.572 ns |  41.285 ns |  1.00 | 0.5264 |      - |   4,464 B |
// |  ToList() |  1000 |  5,497.45 ns |  48.618 ns |  45.478 ns |  0.97 | 0.5188 | 0.0076 |   4,344 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() |  1337 |  7,531.18 ns |  62.457 ns |  58.422 ns |  1.00 | 0.8545 | 0.0153 |   7,216 B |
// |  ToList() |  1337 |  7,324.47 ns |  24.170 ns |  21.426 ns |  0.97 | 1.0071 | 0.0305 |   8,464 B |
// |           |       |              |            |            |       |        |        |           |
// | ToArray() | 10000 | 50,839.94 ns | 472.925 ns | 442.374 ns |  1.00 | 6.3477 | 0.5493 |  53,432 B |
// |  ToList() | 10000 | 52,150.84 ns | 211.711 ns | 176.788 ns |  1.02 | 7.8125 | 1.5259 |  65,880 B |
        }


        [Benchmark(Description = "ToArray()", Baseline = true)]
        public int[] ToArray() => Items.ToArray();

        [Benchmark(Description = "ToList()")]
        public List<int> ToList() => Items.ToList();

        // public static void Main() => BenchmarkRunner.Run<BnchLinqToArrayVsToList>();
    }

}