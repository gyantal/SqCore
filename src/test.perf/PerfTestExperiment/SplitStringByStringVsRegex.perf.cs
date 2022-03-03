using BenchmarkDotNet.Attributes;

namespace SqBenchmarks
{
    [MemoryDiagnoser] // we need to enable it in explicit way to get Columns for ' Gen X collections per 1 000 Operations' and MemAlloc
    public class BnchSplitStringByStrVsRegex
    {
        readonly string splitThisStr = "bla, bla, bla, this will, benchmark, this.";

        [Benchmark]
        public void SplitStringByString()
        {
            SqCommon.Utils.SplitStringByCommaWithCharArray(splitThisStr);       // mean run:  103.7 ns, allocated: 336 Byte
        }

        [Benchmark]
        public void SplitStringByRegex()
        {
            SqCommon.Utils.SplitStringByCommaWithRegex(splitThisStr);        // mean run:  1,592.2 ns  (15x more).  allocated: 2,832 Byte. (x8 more) Try to avoid RegEx!!!
        }
    }
}