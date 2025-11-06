
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

export function urlEncodeChars(str: string): string { // RegEx implementation 'can be faster', because it is only 1 call. Rather than multiple string-based replace() calls with many RAM reallocations.
  const charEncodingMap: { '&': string; '=': string; } = { '&': '%26', '=': '%3D' }; // Replacing special characters with their URL-encoded equivalents to avoid conflicts during server-side processing. stackoverflow ref: https://stackoverflow.com/questions/16576983/replace-multiple-characters-in-one-replace-call
  str = str.replace(/[&=]/g, (s) => charEncodingMap[s as keyof typeof charEncodingMap]);
  return str;
}

// Validate - The provided year string is a valid year.
// The year must be exactly 4 characters long and the year should be between 1900 and the current year.
export function isValidYear(year: string): boolean {
  const currentYear = new Date().getFullYear();
  const yearInt = parseInt(year, 10);
  return year.length == 4 && yearInt >= 1900 && yearInt <= currentYear;
}

// Validate - The provided month string is a valid month.
// The month must be exactly 2 characters long and the month should be between 1 and 12.
export function isValidMonth(month: string): boolean {
  const monthInt = parseInt(month, 10);
  return month.length == 2 && monthInt >= 1 && monthInt <= 12;
}

// Validate - The provided day string is a valid day for the given date.
// The day must be exactly 2 characters long and the day should be between 1 and the maximum number of days in the month.
export function isValidDay(day: string, date: Date): boolean {
  const dayInt = parseInt(day, 10);
  const maxDays = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate(); // Calculates the maximum days in a month by moving to the next month's 0th day (0 as the day, refers to the last day of the current month)
  return day.length == 2 && dayInt >= 1 && dayInt <= maxDays;
}

export function addEventListenerResizeWidth(chartContainerDiv: HTMLElement) {
  const widthResizerDiv: HTMLElement | null = chartContainerDiv.querySelector('#widthResizer');
  if (widthResizerDiv == null)
    return;

  widthResizerDiv.addEventListener('mousedown', onMouseDownResizeWidth);
  function onMouseDownResizeWidth(event: MouseEvent) {
    const originalMouseX: number = event.pageX;
    const chartDivDomRect: DOMRect = chartContainerDiv.getBoundingClientRect();

    function mousemove(event: MouseEvent) {
      const newWidth: number = chartDivDomRect.width - (originalMouseX - event.pageX);
      const chartDivWidth: number = chartDivDomRect.width;
      const restrictedWidth: number = Math.min(newWidth, chartDivWidth); // Prevent the width from exceeding the container's width
      chartContainerDiv.style.width = (restrictedWidth / chartDivWidth) * 100 + '%';
    }

    function stopResize() {
      window.removeEventListener('mousemove', mousemove);
    }
    window.addEventListener('mousemove', mousemove);
    window.addEventListener('mouseup', stopResize);
  }
}

export function addEventListenerResizeHeight(chartContainerDiv: HTMLElement) {
  const heightResizerDiv: HTMLElement | null = chartContainerDiv.querySelector('#heightResizer');
  if (heightResizerDiv == null)
    return;

  heightResizerDiv.addEventListener('mousedown', onMouseDownResizeHeight);
  function onMouseDownResizeHeight(event: MouseEvent) {
    const originalMouseY: number = event.pageY;
    const chartDivDomRect: DOMRect = chartContainerDiv.getBoundingClientRect();

    function mousemove(event: MouseEvent) {
      const newHeight: number = chartDivDomRect.height - (originalMouseY - event.pageY);
      const chartDivHeight: number = chartDivDomRect.height;
      const restrictedHeight: number = Math.min(newHeight, chartDivHeight); // Prevent the height from exceeding the container's height
      chartContainerDiv.style.height = (restrictedHeight / window.innerHeight) * 100 + 'vh';
    }

    function stopResize() {
      window.removeEventListener('mousemove', mousemove);
    }
    window.addEventListener('mousemove', mousemove);
    window.addEventListener('mouseup', stopResize);
  }
}