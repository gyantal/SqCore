
// There is no Thread.Sleep() functionality in Javacscript.
// We can get the similar behaviour using async/await and setTimeout.
// Below is the example of Sleep function.

export function sleep(time: number) {
  return new Promise((resolve) => {
    setTimeout(resolve, time);
  });
}