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

// |           Method | DataLen |          Mean |       Error |      StdDev | Ratio |   Gen 0 |   Gen 1 | Allocated |
// |----------------- |-------- |--------------:|------------:|------------:|------:|--------:|--------:|----------:|
// |      Linq.Select |       0 |     36.468 ns |   0.2606 ns |   0.2310 ns |  1.00 |  0.0124 |       - |     104 B |
// |     ForOnObjList |       0 |      8.541 ns |   0.0784 ns |   0.0733 ns |  0.23 |  0.0038 |       - |      32 B |
// | ForeachOnObjList |       0 |     10.848 ns |   0.1291 ns |   0.1078 ns |  0.30 |  0.0038 |       - |      32 B |
// |      ForOnObjArr |       0 |      1.745 ns |   0.0329 ns |   0.0291 ns |  0.05 |  0.0029 |       - |      24 B |
// |   ForOnStructArr |       0 |      1.816 ns |   0.0301 ns |   0.0267 ns |  0.05 |  0.0029 |       - |      24 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |       1 |     43.488 ns |   0.2323 ns |   0.2173 ns |  1.00 |  0.0191 |       - |     160 B |
// |     ForOnObjList |       1 |     12.208 ns |   0.1022 ns |   0.0956 ns |  0.28 |  0.0105 |       - |      88 B |
// | ForeachOnObjList |       1 |     16.958 ns |   0.1860 ns |   0.1649 ns |  0.39 |  0.0105 |       - |      88 B |
// |      ForOnObjArr |       1 |      6.655 ns |   0.1383 ns |   0.1226 ns |  0.15 |  0.0067 |       - |      56 B |
// |   ForOnStructArr |       1 |      2.488 ns |   0.0541 ns |   0.0506 ns |  0.06 |  0.0038 |       - |      32 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |       6 |     67.065 ns |   0.6557 ns |   0.5119 ns |  1.00 |  0.0381 |       - |     320 B |
// |     ForOnObjList |       6 |     36.116 ns |   0.4054 ns |   0.3594 ns |  0.54 |  0.0296 |       - |     248 B |
// | ForeachOnObjList |       6 |     51.494 ns |   0.5945 ns |   0.5561 ns |  0.77 |  0.0296 |       - |     248 B |
// |      ForOnObjArr |       6 |     26.992 ns |   0.2051 ns |   0.1818 ns |  0.40 |  0.0258 |       - |     216 B |
// |   ForOnStructArr |       6 |      5.947 ns |   0.0400 ns |   0.0374 ns |  0.09 |  0.0057 |       - |      48 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |      10 |     92.606 ns |   0.6391 ns |   0.5666 ns |  1.00 |  0.0535 |  0.0001 |     448 B |
// |     ForOnObjList |      10 |     60.374 ns |   0.3902 ns |   0.3459 ns |  0.65 |  0.0449 |  0.0001 |     376 B |
// | ForeachOnObjList |      10 |     85.998 ns |   0.6782 ns |   0.6012 ns |  0.93 |  0.0449 |  0.0001 |     376 B |
// |      ForOnObjArr |      10 |     43.369 ns |   0.1226 ns |   0.1087 ns |  0.47 |  0.0411 |  0.0001 |     344 B |
// |   ForOnStructArr |      10 |      8.979 ns |   0.0664 ns |   0.0589 ns |  0.10 |  0.0076 |       - |      64 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |      39 |    275.368 ns |   2.1582 ns |   2.0188 ns |  1.00 |  0.1645 |  0.0014 |   1,376 B |
// |     ForOnObjList |      39 |    192.216 ns |   1.3441 ns |   1.1915 ns |  0.70 |  0.1557 |  0.0014 |   1,304 B |
// | ForeachOnObjList |      39 |    277.897 ns |   2.1260 ns |   1.8846 ns |  1.01 |  0.1554 |  0.0014 |   1,304 B |
// |      ForOnObjArr |      39 |    170.586 ns |   3.2658 ns |   3.4943 ns |  0.62 |  0.1519 |  0.0014 |   1,272 B |
// |   ForOnStructArr |      39 |     29.762 ns |   0.4803 ns |   0.4493 ns |  0.11 |  0.0220 |       - |     184 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |     100 |    592.703 ns |   4.9878 ns |   4.4215 ns |  1.00 |  0.3977 |  0.0095 |   3,328 B |
// |     ForOnObjList |     100 |    478.343 ns |   4.4946 ns |   4.2043 ns |  0.81 |  0.3891 |  0.0091 |   3,256 B |
// | ForeachOnObjList |     100 |    679.658 ns |   2.2509 ns |   1.9953 ns |  1.15 |  0.3891 |  0.0086 |   3,256 B |
// |      ForOnObjArr |     100 |    405.812 ns |   3.3285 ns |   2.9506 ns |  0.68 |  0.3853 |  0.0091 |   3,224 B |
// |   ForOnStructArr |     100 |     78.718 ns |   1.4036 ns |   1.3129 ns |  0.13 |  0.0507 |       - |     424 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |     666 |  3,772.060 ns |  14.4814 ns |  12.8374 ns |  1.00 |  2.5597 |  0.3281 |  21,440 B |
// |     ForOnObjList |     666 |  3,170.079 ns |  20.8241 ns |  18.4600 ns |  0.84 |  2.5520 |  0.3166 |  21,368 B |
// | ForeachOnObjList |     666 |  4,485.960 ns |  34.2815 ns |  32.0670 ns |  1.19 |  2.5482 |  0.3128 |  21,368 B |
// |      ForOnObjArr |     666 |  2,707.709 ns |  20.2349 ns |  17.9377 ns |  0.72 |  2.5482 |  0.3166 |  21,336 B |
// |   ForOnStructArr |     666 |    476.948 ns |   6.3435 ns |   5.6234 ns |  0.13 |  0.3204 |  0.0029 |   2,688 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |    1000 |  5,691.712 ns |  25.5326 ns |  22.6340 ns |  1.00 |  3.8376 |  0.6485 |  32,128 B |
// |     ForOnObjList |    1000 |  4,793.336 ns |  17.9553 ns |  15.9169 ns |  0.84 |  3.8300 |  0.6485 |  32,056 B |
// | ForeachOnObjList |    1000 |  6,998.193 ns |  28.6676 ns |  25.4131 ns |  1.23 |  3.8300 |  0.6485 |  32,056 B |
// |      ForOnObjArr |    1000 |  4,129.911 ns |  25.8939 ns |  22.9543 ns |  0.73 |  3.8223 |  0.6332 |  32,024 B |
// |   ForOnStructArr |    1000 |    704.512 ns |   6.3611 ns |   5.6389 ns |  0.12 |  0.4807 |  0.0067 |   4,024 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |    1337 |  7,645.676 ns |  70.5076 ns |  62.5032 ns |  1.00 |  5.1270 |  1.0376 |  42,912 B |
// |     ForOnObjList |    1337 |  6,474.703 ns |  30.3026 ns |  26.8625 ns |  0.85 |  5.1193 |  1.0223 |  42,840 B |
// | ForeachOnObjList |    1337 |  9,385.328 ns |  65.6321 ns |  54.8058 ns |  1.23 |  5.1117 |  1.0223 |  42,840 B |
// |      ForOnObjArr |    1337 |  5,590.121 ns |  50.4048 ns |  44.6826 ns |  0.73 |  5.1117 |  1.2741 |  42,808 B |
// |   ForOnStructArr |    1337 |    935.202 ns |   5.6073 ns |   4.9707 ns |  0.12 |  0.6409 |  0.0114 |   5,376 B |
// |                  |         |               |             |             |       |         |         |           |
// |      Linq.Select |   10000 | 69,100.129 ns | 556.0135 ns | 492.8915 ns |  1.00 | 38.2080 | 18.9209 | 320,128 B |
// |     ForOnObjList |   10000 | 59,334.170 ns | 324.8883 ns | 303.9007 ns |  0.86 | 38.1470 | 15.2588 | 320,056 B |
// | ForeachOnObjList |   10000 | 79,212.381 ns | 408.3835 ns | 362.0213 ns |  1.15 | 38.0859 | 15.2588 | 320,056 B |
// |      ForOnObjArr |   10000 | 55,850.769 ns | 392.4516 ns | 367.0995 ns |  0.81 | 38.1470 | 15.2588 | 320,024 B |
// |   ForOnStructArr |   10000 |  6,801.443 ns |  27.1639 ns |  25.4092 ns |  0.10 |  4.7607 |  0.5951 |  40,024 B |

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
// LINQ takes +15% less than foreach on class-list. Avoid foreach!
// LINQ takes +27% more than for on class-list 
// LINQ takes +48% more than for on class-array
// LINQ takes +8x-10x more than for on struct-array

// So, 1. Use 25% faster For instead of ForEach. 2. use Array if possible. Furthermore, use Struct with arrays. That is the fastest.

// 4. Case study of 1000 long list:
// If you write LINQ like : resultList = inputList.Select(r => new ClassB() { Data = r.Data + 10 }).ToList();
// For a List<Class>, that takes 6,000ns. = 6us (micro-second), which is significant, when 1 ms is a huge time waste in general.
// If you write Foreach loop with List<class>, it takes 7,000ns. Even worse than LINQ.
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

// 7.The For vs. Foreach battle is not decided. Sometimes this is better, sometimes the other.
// >https://stackoverflow.com/questions/1124753/performance-difference-for-control-structures-for-and-foreach-in-c-sharp
// "A for loop gets compiled to code approximately equivalent to this:
// int tempCount = 0;
// while (tempCount < list.Count)
// {
//     if (list[tempCount].value == value)
//     {
//         // Do something
//     }
//     tempCount++;
// }
// Where as a foreach loop gets compiled to code approximately equivalent to this:
// using (IEnumerator<T> e = list.GetEnumerator())
// {
//     while (e.MoveNext())
//     {
//         T o = (MyClass)e.Current;
//         if (row.value == value)
//         {
//             // Do something
//         }
//     }
// }"
// >https://www.c-sharpcorner.com/article/c-sharp-performance-of-code-for-loop-vs-for-each-loop/
// "The foreach loop took 107 milliseconds to execute the same process while the classic for loop took 14 milliseconds"
// "If we have to access the local variable value multiple times in the for loop, in that case, the performance will decrease."
// >https://stackoverflow.com/questions/365615/in-net-which-loop-runs-faster-for-or-foreach
// "for loops on List are a bit more than 2 times cheaper than foreach loops on List.
// Looping on array is around 2 times cheaper than looping on List.
// As a consequence, looping on array using for is 5 times cheaper than looping on List using foreach (which I believe, is what we all do)."
// >But others found negligible difference. Or that sometimes Foreach was faster.
// https://stackoverflow.com/questions/365615/in-net-which-loop-runs-faster-for-or-foreach

// 8. The For vs. Foreach battle: Advantage of foreach (sometimes)
// Foreach() is slower than for(), but it can warn List<> modifications in multithreaded environment. 
// That can be very useful, so bugs are immediately appear, and not like randomly (once a month) in the future.
// foreach (var item in itemsList)
// 	// will throw exception "Collection was modified; enumeration operation may not execute." if another thread Add(), Remove() from the list.
// for (int i = 0; i < itemsList.Count; i++) 
// for (int i = itemsList.Count - 1; i >= 0; i--) 
// 	// will not throw exception. If another thread Add(), Remove() from the list, weird things can happen: Skipping one item, or processing some items twice.
// Therefore, if the List is accessed by multiple threads, use foreach() in general. Even though it is slower than for().

// Brief Conclusion:
// - Avoid foreach. Even worse than LINQ.Select (7000ns). But used foreach() if the List is accessed by multiple threads and there is a possibility of Add()/Remove(). It will warn if misused.
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

        public Random rand = new();
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
            List<ClassB> result = new(len);
            for (int i = 0; i < len; i++)
            {
                result.Add(new ClassB() { Data = InputObjList[i].Data + 10 });
            }
            return result;
        }

        [Benchmark(Description = "ForeachOnObjList")]
        public List<ClassB> ForeachOnObjList()
        {
            int len = InputObjList.Count;
            List<ClassB> result = new(len);
            foreach (var item in InputObjList)
            {
                result.Add(new ClassB() { Data = item.Data + 10 });
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



        public static void RenameThisToMain_StopwatchBenchmark(string[] _)
        {
            Random r = new();

            int dataLen = 100;
            List<ClassA> inputObjList = new(dataLen);
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
            List<ClassB> resultImp = new(dataLen);
            for (int i = 0; i < dataLen; i++)
            {
                resultImp.Add(new ClassB() { Data = inputObjList[i].Data + 10 });
            }
            swi1.Stop();
            Console.WriteLine("Time Taken for for list of Class: (1st iteration): {0}us", (swi1.Elapsed.TotalMilliseconds) * 1000);
            Console.WriteLine($"Write some of the results, so compiler cannot overoptimize: '{resultImp[0].Data}'");

            Stopwatch swi2 = Stopwatch.StartNew();
            List<ClassB> resultImp2 = new(dataLen);
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