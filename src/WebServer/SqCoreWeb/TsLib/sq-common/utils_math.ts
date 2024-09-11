import { MonthlySeasonality } from './backtestCommon';
import { AnnualReturn } from './backtestStatistics';

export function sqGaussian(mean: number, stdev: number): any {
  // returns a gaussian random function with the given mean and stdev.
  let y2;
  let useLast = false;
  return function() {
    let y1;
    if (useLast) {
      y1 = y2;
      useLast = false;
    } else {
      let x1; let x2; let w;
      do {
        x1 = 2.0 * Math.random() - 1.0;
        x2 = 2.0 * Math.random() - 1.0;
        w = x1 * x1 + x2 * x2;
      } while (w >= 1.0);
      w = Math.sqrt((-2.0 * Math.log(w)) / w);
      y1 = x1 * w;
      y2 = x2 * w;
      useLast = true;
    }

    const retval = mean + stdev * y1;
    return retval;
  };
}

export function sqAverage(arrayB: any): number {
  let total = 0;
  for (let i = 0; i < arrayB.length; i++)
    total += arrayB[i];
  const avg = total / arrayB.length;
  return avg;
}

export function sqMedian(arrayB: any): number {
  arrayB.sort(function(a: number, b: number) {
    return a - b;
  });
  const i = arrayB.length / 2;
  let med: number;
  i % 1 === 0 ? med = (arrayB[Math.floor(i) - 1] + arrayB[Math.floor(i)]) / 2 : med = arrayB[Math.floor(i)];
  return med;
}

export function sqStdDev(arrayB: any): number {
  const avg = sqAverage(arrayB);
  let sumdev = 0;
  for (let i = 0; i < arrayB.length; i++)
    sumdev += (arrayB[i]-avg)*(arrayB[i]-avg);
  const stdDev = Math.sqrt(sumdev/(arrayB.length-1));
  return stdDev;
}

// Function to calculate average for a given number of years
export function sqAverageOfSeasonalityData(monthlySeasonality: MonthlySeasonality[], years: number): number[] {
  const avgArray: number[] = new Array(12).fill(NaN);

  for (let i = 0; i < 12; i++) {
    let returnsSum: number = 0;
    let returnsNum: number = 0; // number of items summed

    for (let j = 0; j < years; j++) { // Selecting only the data from the last 'years' years
      if (monthlySeasonality[j].returns[i] != undefined && !Number.isNaN(monthlySeasonality[j].returns[i])) {
        returnsSum += monthlySeasonality[j].returns[i];
        returnsNum++;
      }
    }
    avgArray[i] = returnsSum / returnsNum;
  }
  return avgArray;
}

export function sqAverageOfAunnalReturns(annualReturns: AnnualReturn[], years: number): number {
  let avgRet = 0;
  let returnsSum: number = 0;
  let returnsNum: number = 0; // number of items summed

  for (let j = 0; j < years; j++) { // Selecting only the data from the last 'years' years
    if (annualReturns[j].return != undefined && !Number.isNaN(annualReturns[j].return)) {
      returnsSum += annualReturns[j].return;
      returnsNum++;
    }
  }
  avgRet = returnsSum/returnsNum;
  return avgRet;
}