
// There is no Thread.Sleep() functionality in Javacscript.
// We can get the similar behaviour using async/await and setTimeout.
// Below is the example of Sleep function.
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
  const seenDictObj = {}; // It is a JS object, used as a Dictionary;
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