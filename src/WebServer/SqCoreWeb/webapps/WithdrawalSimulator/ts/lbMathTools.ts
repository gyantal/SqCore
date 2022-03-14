export function lbgaussian(mean: number, stdev: number): any {
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

export function lbaverage(arrayB: any): number {
  let total = 0;
  for (let i = 0; i < arrayB.length; i++)
    total += arrayB[i];
  const avg = total / arrayB.length;
  return avg;
}

export function lbmedian(arrayB: any): number {
  arrayB.sort(function(a: number, b: number) {
    return a - b;
  });
  const i = arrayB.length / 2;
  let med: number;
  i % 1 === 0 ? med = (arrayB[Math.floor(i) - 1] + arrayB[Math.floor(i)]) / 2 : med = arrayB[Math.floor(i)];
  return med;
}
export function lbstdDev(arrayB: any): number {
  const avg = lbaverage(arrayB);
  let sumdev = 0;
  for (let i = 0; i < arrayB.length; i++)
    sumdev += (arrayB[i]-avg)*(arrayB[i]-avg);
  const stdDev = Math.sqrt(sumdev/(arrayB.length-1));
  return stdDev;
}