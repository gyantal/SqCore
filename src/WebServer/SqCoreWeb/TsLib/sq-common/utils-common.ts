
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