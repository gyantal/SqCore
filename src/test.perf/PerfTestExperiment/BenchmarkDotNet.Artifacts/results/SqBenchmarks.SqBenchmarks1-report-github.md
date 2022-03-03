``` ini

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
AMD Ryzen 9 5900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.200
  [Host]     : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT
  DefaultJob : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT


```
|     Method | start | end |          Mean |     Error |    StdDev |  Gen 0 | Allocated |
|----------- |------ |---- |--------------:|----------:|----------:|-------:|----------:|
| **EmptyArray** |     **?** |   **?** |     **0.0124 ns** | **0.0064 ns** | **0.0057 ns** |      **-** |         **-** |
| EightBytes |     ? |   ? |     2.4734 ns | 0.0337 ns | 0.0315 ns | 0.0019 |      32 B |
|   **SomeLinq** |     **0** | **100** |   **565.4990 ns** | **1.9883 ns** | **1.6603 ns** | **0.0286** |     **488 B** |
|   **SomeLinq** |     **0** | **200** | **1,031.3137 ns** | **5.5684 ns** | **5.2087 ns** | **0.0362** |     **624 B** |
