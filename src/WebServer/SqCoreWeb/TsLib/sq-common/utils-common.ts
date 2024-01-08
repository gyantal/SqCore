
// There is no Thread.Sleep() functionality in Javacscript.
// We can get the similar behaviour using async/await and setTimeout.
// Below is the example of Sleep function. This is how to use it: "await sleep(5000);"
export function sleep(time: number) {
  return new Promise((resolve) => {
    setTimeout(resolve, time);
  });
}

export function dateTimeToDate(date: Date): Date { // getting the Date component, without Time. Equivalent to C# DateTime.Date property.
  const dateNum: number = date.getTime();
  return new Date(dateNum - (dateNum % 86400000));
}

// Event listener for when element becomes visible? https://stackoverflow.com/questions/1462138/event-listener-for-when-element-becomes-visible
// Use it like this: onVisible(document.querySelector("#myElement"), () => console.log("it's visible"));
export function onFirstVisibleEventListener(element : Element | null, callback: (element: Element | null) => void) {
  if (element == null)
    return;
  new IntersectionObserver((entries, observer) => {
    entries.forEach((entry) => {
      if (entry.intersectionRatio > 0) {
        callback(element);
        observer.disconnect();
      }
    });
  }).observe(element);
}

// remove duplicate values from an array? https://stackoverflow.com/questions/9229645/remove-duplicate-values-from-js-array
export function removeDuplicates(inArr: string[]): string[] {
  const seenDictObj: { [key: string]: number } = {}; // It is a JS object, used as a Dictionary;
  const outArr: string[] = [];
  const len: number = inArr.length;
  let j: number = 0;
  for (let i = 0; i < len; i++) {
    const item = inArr[i];
    if (seenDictObj[item] !== 1) {
      seenDictObj[item] = 1;
      outArr[j++] = item;
    }
  }
  return outArr;
}

// Conver the numder to a date format (eg: 20020830 => Fri Aug 30 2002 00:00:00 GMT+0530 (India Standard Time))
export function parseNumberToDate(dateInt: number): Date {
  const day = Math.floor(dateInt % 100);
  const month = Math.floor((dateInt / 100) % 100) - 1; // in Javacript the month parameter is zero-based, meaning January is represented by 0, February by 1, and so on, with December being 11. This can sometimes lead to unexpected results when constructing dates. So we have substract the value by 1 (eg: Month - 1)
  const year = Math.floor(dateInt / 10000);
  return new Date(year, month, day);
}