using BenchmarkDotNet.Attributes;

using System.Linq;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace SqBenchmarks
{
// CPU Performance: Data Transformation: LINQ vs. For-loop

// see corresponding gDoc: https://docs.google.com/document/d/1T8vwk82VSumAqSxPgvRx3OyOyGS6ZpB_Q3maXZ45YaU

// Job: Data-transformation: 
// Imagine the task of transforming a list of objects to another list of objects.
// For example, collecting all the SqTicker strings from the List of Assets. 
// Something like this:
// resultList = inputList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
// Assume that there is no Where() clause, so the length of the input array is the same as the length of the output array. (see Caveat later)

// Here are the performance measurements made by BenchmarkDotNet (Release).
// Note: BenchmarkDotNet makes it very hard to accidentally do Debug runs.

// |         Method | DataLen |          Mean |       Error |      StdDev | Ratio |   Gen 0 |   Gen 1 | Allocated |
// |--------------- |-------- |--------------:|------------:|------------:|------:|--------:|--------:|----------:|
// |    Linq.Select |       0 |     38.793 ns |   0.4550 ns |   0.4033 ns |  1.00 |  0.0124 |       - |     104 B |
// |   ForOnObjList |       0 |      8.283 ns |   0.0790 ns |   0.0739 ns |  0.21 |  0.0038 |       - |      32 B |
// |    ForOnObjArr |       0 |      1.937 ns |   0.0576 ns |   0.0538 ns |  0.05 |  0.0029 |       - |      24 B |
// | ForOnStructArr |       0 |      1.865 ns |   0.0703 ns |   0.0657 ns |  0.05 |  0.0029 |       - |      24 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |       1 |     39.425 ns |   0.1653 ns |   0.1465 ns |  1.00 |  0.0191 |       - |     160 B |
// |   ForOnObjList |       1 |     12.766 ns |   0.2596 ns |   0.2428 ns |  0.32 |  0.0105 |       - |      88 B |
// |    ForOnObjArr |       1 |      6.261 ns |   0.0903 ns |   0.0801 ns |  0.16 |  0.0067 |       - |      56 B |
// | ForOnStructArr |       1 |      2.763 ns |   0.0357 ns |   0.0334 ns |  0.07 |  0.0038 |       - |      32 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |       6 |     69.984 ns |   0.5657 ns |   0.5292 ns |  1.00 |  0.0381 |       - |     320 B |
// |   ForOnObjList |       6 |     35.861 ns |   0.5386 ns |   0.5038 ns |  0.51 |  0.0296 |       - |     248 B |
// |    ForOnObjArr |       6 |     27.466 ns |   0.2438 ns |   0.2281 ns |  0.39 |  0.0258 |       - |     216 B |
// | ForOnStructArr |       6 |      6.043 ns |   0.0876 ns |   0.0819 ns |  0.09 |  0.0057 |       - |      48 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |      10 |    101.930 ns |   1.0724 ns |   1.0031 ns |  1.00 |  0.0535 |  0.0001 |     448 B |
// |   ForOnObjList |      10 |     61.854 ns |   0.7295 ns |   0.6824 ns |  0.61 |  0.0449 |  0.0001 |     376 B |
// |    ForOnObjArr |      10 |     43.847 ns |   0.3506 ns |   0.2927 ns |  0.43 |  0.0411 |  0.0001 |     344 B |
// | ForOnStructArr |      10 |      9.142 ns |   0.1075 ns |   0.0953 ns |  0.09 |  0.0076 |       - |      64 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |      39 |    270.856 ns |   1.2153 ns |   1.0774 ns |  1.00 |  0.1645 |  0.0014 |   1,376 B |
// |   ForOnObjList |      39 |    195.170 ns |   0.9391 ns |   0.7332 ns |  0.72 |  0.1557 |  0.0014 |   1,304 B |
// |    ForOnObjArr |      39 |    173.261 ns |   1.9927 ns |   1.7664 ns |  0.64 |  0.1519 |  0.0014 |   1,272 B |
// | ForOnStructArr |      39 |     30.342 ns |   0.3052 ns |   0.2854 ns |  0.11 |  0.0220 |       - |     184 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |     100 |    620.678 ns |   2.8486 ns |   2.3787 ns |  1.00 |  0.3977 |  0.0095 |   3,328 B |
// |   ForOnObjList |     100 |    487.939 ns |   7.1035 ns |   6.2971 ns |  0.78 |  0.3891 |  0.0086 |   3,256 B |
// |    ForOnObjArr |     100 |    418.686 ns |   1.6983 ns |   1.5055 ns |  0.67 |  0.3853 |  0.0091 |   3,224 B |
// | ForOnStructArr |     100 |     79.951 ns |   0.7215 ns |   0.6748 ns |  0.13 |  0.0507 |       - |     424 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |     666 |  3,987.387 ns |  24.6445 ns |  21.8467 ns |  1.00 |  2.5558 |  0.3204 |  21,440 B |
// |   ForOnObjList |     666 |  3,224.699 ns |  48.1311 ns |  45.0218 ns |  0.81 |  2.5520 |  0.3166 |  21,368 B |
// |    ForOnObjArr |     666 |  2,765.155 ns |  18.5471 ns |  16.4415 ns |  0.69 |  2.5482 |  0.3166 |  21,336 B |
// | ForOnStructArr |     666 |    482.252 ns |   2.6599 ns |   2.2212 ns |  0.12 |  0.3204 |  0.0029 |   2,688 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |    1000 |  6,023.477 ns |  47.2371 ns |  44.1856 ns |  1.00 |  3.8376 |  0.6485 |  32,128 B |
// |   ForOnObjList |    1000 |  4,868.348 ns |  12.8791 ns |  10.7546 ns |  0.81 |  3.8300 |  0.6485 |  32,056 B |
// |    ForOnObjArr |    1000 |  4,180.904 ns |  38.1239 ns |  35.6611 ns |  0.69 |  3.8223 |  0.6332 |  32,024 B |
// | ForOnStructArr |    1000 |    715.218 ns |   4.8852 ns |   4.5696 ns |  0.12 |  0.4807 |  0.0067 |   4,024 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |    1337 |  8,150.741 ns |  55.7886 ns |  49.4551 ns |  1.00 |  5.1270 |  1.0376 |  42,912 B |
// |   ForOnObjList |    1337 |  6,636.393 ns |  34.6983 ns |  32.4568 ns |  0.81 |  5.1193 |  1.0223 |  42,840 B |
// |    ForOnObjArr |    1337 |  5,695.062 ns |  69.2681 ns |  61.4044 ns |  0.70 |  5.1117 |  1.2741 |  42,808 B |
// | ForOnStructArr |    1337 |    955.824 ns |   8.2955 ns |   7.3538 ns |  0.12 |  0.6409 |  0.0114 |   5,376 B |
// |                |         |               |             |             |       |         |         |           |
// |    Linq.Select |   10000 | 69,890.422 ns | 613.3549 ns | 573.7326 ns |  1.00 | 38.2080 | 18.9209 | 320,128 B |
// |   ForOnObjList |   10000 | 59,838.902 ns | 298.4527 ns | 264.5705 ns |  0.86 | 38.1470 | 15.2588 | 320,056 B |
// |    ForOnObjArr |   10000 | 54,131.012 ns | 665.6403 ns | 622.6404 ns |  0.77 | 38.1470 | 15.2588 | 320,024 B |
// | ForOnStructArr |   10000 |  6,888.553 ns |  40.7039 ns |  36.0829 ns |  0.10 |  4.7607 |  0.5951 |  40,024 B |

// See code in GitHubRepos\SqCore\src\test.perf\PerfTestExperiment\LinqSelectVsFor.perf.cs


// >Conclusions: 
// 1. LINQ start is horrible when it runs first. 2000x worse than later runs (as it has to load LINQ libraries to RAM), but that is fine later.
// Time Taken for First LINQ: 578.2us = 578,000 ns
// Time Taken for 100th LINQ: 3.153us
// Time Taken for 1000th LINQ: 2.7521us

// 2. Another conclusion is that we see from "list of Class: (1st-2nd iteration)": 
// The first run: 2,000ns vs. the second run: 6,200 ns, that measurement is very noisy.
// We cannot draw conclusions with a simple 'running it once' approach. We have to run the same function 1000x times.
// We have to use a system like BenchmarkDotNet that runs tests many times and measures the volatility.

// 3. Later runs: scrutinize the measurements with DataLength = 100 or 666 as those are the most typical.
// LINQ takes +27% more than the List of Class. 
// LINQ takes +48% more than the Array of Class.
// LINQ takes +8x-10x more than the Array of Struct.

// So, use Array if possible. Furthermore, use Struct with arrays. That is the fastest.

// 4. Case study of 1000 long list:
// If you write LINQ like : resultList = inputList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
// For a List<Class>, that takes 6,000ns. = 6us (micro-second), which is significant, when 1 ms is a huge time waste in general.
// If you write For loop with List<class>, it takes 4,800ns.
// If you write For loop with Array<class>, it takes 4,100ns.
// If you write For loop with Array<struct>, it takes 700ns. (about 9x less)

// 5. So, think about a code on the webserver. If it runs only once per user web-query, then you can use LINQ, 
// because of quick programming, fewer bugs, simpler and clearer code. These are the advantages when LINQ usage is justified.
// BUT if data transformation runs 10x - 100x inside a loop, then implement it with a for() loop over a struct array.
// Is a general rule of thumb: In MemDb classes, for efficiency try to implement for() loop over struct array. In webserver query serving code, you can use LINQ.

// 6. Caveat: if there is a Where() that doesn't significantly change the advice here.
// The most optimal implementation would be to do 3 steps. Avoid many re-allocations.
// 	Step 1: Iterating over the source array, counting the number of output objects, which fulfill the Where() clause
// 	Step 2: Only 1 mem-alloc. Creating the final output array size as one command.
// 	Step 3: Iterating over the source array and populate the output array.
// But it is not necessary to implement this fastest method all the time. If the code runs only sporadically, maintaining simple and clean code has a higher priority.


// Brief Conclusion:
// - Use struct arrays if possible. That is the fastest. (700ns)
// - If you have to use Classes, no structs (like Asset classes), then use arrays of Classes (4,100ns)
// - Use Arrays over List. Arrays are +20% faster than List. Because Array implements IEnumerable<T>, LINQ can use arrays as well, not only list. Just think about the length of the data. If it can change the size, then use List. But 90% of the time that list never changes length. So, you should use Arrays.
// - Use imperative for loops on an array of structs (700ns, which is 8-10x faster), over LINQ (6,000ns). 

    public class ClassA
    {
        public int Data { get; set; } = 0;
    }

    public class ClassB
    {
        public int Data { get; set; } = 0;
    }

    public struct StructA
    {
        public int Data { get; set; }
    }

    public struct StructB
    {
        public int Data { get; set; }
    }

    [MemoryDiagnoser]
    public class BnchLinqSelectVsFor
    {
        [Params(0, 1, 6, 10, 39, 100, 666, 1000, 1337, 10000)]
        //[Params(0, 1, 6, 10, 39, 100)]
        public int DataLen { get; set; }

        public Random rand = new Random();
        public List<ClassA> InputObjList;
        public ClassA[] InputObjArr;
        public StructA[] InputStructArr;

        [GlobalSetup]
        public void GlobalSetup()   // executed once per each Params value
        {
            InputObjList = Enumerable.Range(0, DataLen).Select(r => new ClassA() { Data = rand.Next() }).ToList();
            InputObjArr = Enumerable.Range(0, DataLen).Select(r => new ClassA() { Data = rand.Next() }).ToArray();
            InputStructArr = Enumerable.Range(0, DataLen).Select(r => new StructA() { Data = rand.Next() }).ToArray();
        }


        [Benchmark(Description = "Linq.Select", Baseline = true)]
        public List<ClassB> LinqSelect() 
        {
            return InputObjList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
        }

        [Benchmark(Description = "ForOnObjList")]
        public List<ClassB> ForLoopOnObjList()
        {
            int len = InputObjList.Count;
            List<ClassB> result = new List<ClassB>(len);
            for (int i = 0; i < len; i++)
            {
                result.Add(new ClassB() { Data = InputObjList[i].Data + 10 });
            }
            return result;
        }

        [Benchmark(Description = "ForOnObjArr")]
        public ClassB[] ForLoopOnObjArr()
        {
            int len = InputObjArr.Length;
            ClassB[] result = new ClassB[len];
            for (int i = 0; i < len; i++)
            {
                result[i] = new ClassB() { Data = InputObjArr[i].Data + 10 };
            }
            return result;
        }

        [Benchmark(Description = "ForOnStructArr")]
        public StructB[] ForLoopOnStructArr()
        {
            int len = InputStructArr.Length;
            StructB[] result = new StructB[len];
            for (int i = 0; i < len; i++)
            {
                result[i].Data = InputStructArr[i].Data + 15;
            }
            return result;
        }



        public static void RenameThisToMain_StopwatchBenchmark(string[] args)
        {
            Random r = new Random();

            int dataLen = 100;
            List<ClassA> inputObjList = new List<ClassA>(dataLen);
            for (int i = 0; i < dataLen; i++)
            {
                inputObjList.Add(new ClassA() { Data = r.Next() }); // make it random.
            }

            ClassA[] inputObjArr = new ClassA[dataLen];
            for (int i = 0; i < inputObjArr.Length; i++)
            {
                inputObjArr[i] = new ClassA() { Data = r.Next() };
            }

            StructA[] inputStructArr = new StructA[dataLen];
            for (int i = 0; i < inputStructArr.Length; i++)
            {
                 inputStructArr[i] = new StructA() { Data = r.Next() };
            }


            // Execution time calculation for LINQ
            Stopwatch sw1 = Stopwatch.StartNew();
            List<ClassB> resultLinq = inputObjList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
            sw1.Stop();
            Console.WriteLine("Time Taken for First LINQ: {0}us", (sw1.Elapsed.TotalMilliseconds) * 1000);

            Stopwatch sw2 = Stopwatch.StartNew();
            int nIterations1 = 100;
            List<ClassB> resultLinq1 = null;
            for (int i = 0; i < nIterations1; i++)
            {
                resultLinq1 = inputObjList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
            }
            sw2.Stop();
            Console.WriteLine("Time Taken for 100th LINQ: {0}us", ((sw2.Elapsed.TotalMilliseconds) * 1000) / nIterations1);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultLinq1[0].Data}'");

            Stopwatch sw3 = Stopwatch.StartNew();
            int nIterations2 = 1000;
            List<ClassB> resultLinq2 = null;
            for (int i = 0; i < nIterations2; i++)
            {
                resultLinq2 = inputObjList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
            }
            sw3.Stop();

            Console.WriteLine("Time Taken for 1000th LINQ: {0}us", ((sw3.Elapsed.TotalMilliseconds) * 1000) / nIterations2);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultLinq2[0].Data}'");

            // Execution time calculation using for loop
            Stopwatch swi1 = Stopwatch.StartNew();
            List<ClassB> resultImp = new List<ClassB>(dataLen);
            for (int i = 0; i < dataLen; i++)
            {
                resultImp.Add(new ClassB() { Data = inputObjList[i].Data + 10 });
            }
            swi1.Stop();
            Console.WriteLine("Time Taken for for list of Class: (1st iteration): {0}us", (swi1.Elapsed.TotalMilliseconds) * 1000);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultImp[0].Data}'");

            Stopwatch swi2 = Stopwatch.StartNew();
            List<ClassB> resultImp2 = new List<ClassB>(dataLen);
            for (int i = 0; i < dataLen; i++)
            {
                resultImp2.Add(new ClassB() { Data = inputObjList[i].Data + 10 });
            }
            swi2.Stop();
            Console.WriteLine("Time Taken for for list of Class: (2nd iteration): {0}us", (swi2.Elapsed.TotalMilliseconds) * 1000);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultImp2[0].Data}'");


            // Time calculation using array of classes
            Stopwatch sw_a = Stopwatch.StartNew();
            ClassB[] resultObjArr = new ClassB[dataLen];
            for (int i = 0; i < inputObjArr.Length; i++)
            {
                resultObjArr[i] = new ClassB() { Data = inputObjArr[i].Data + 15 };
            }
            sw_a.Stop();
            Console.WriteLine("Time Taken for array of Class: {0}us",(sw_a.Elapsed.TotalMilliseconds) * 1000);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultObjArr[0].Data}'");

            // Time calculation using array of structs
            Stopwatch sw_s = Stopwatch.StartNew();
            StructB[] resultStructArr = new StructB[dataLen];
            for (int i = 0; i < inputStructArr.Length; i++)
            {
                resultStructArr[i].Data = inputStructArr[i].Data + 15;
             }
            sw_s.Stop();
            Console.WriteLine("Time Taken for array of Struct: {0}us",(sw_s.Elapsed.TotalMilliseconds) * 1000);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultStructArr[0].Data}'");
        }

    }

}