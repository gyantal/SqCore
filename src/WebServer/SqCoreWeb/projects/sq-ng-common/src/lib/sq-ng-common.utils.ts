export class SqNgCommonUtils {
  constructor() { }

  public static getUrlQueryParamsArray(): string[][] {
    return window.location.search.length === 0 ?
      [] :
      window.location.search
          .substr(1)
          .split('&')
          .map((pairString) => pairString.split('=')); // map() creates string[][], which is an array [] of string pairs (string[2])
  }

  public static Array2Obj(array: string[][]): object {
    return array.reduce((out, pair) => {
      out[pair[0]] = pair[1];
      return out;
    }, {});
    // }, {} as Params);  // if you want a typed object, but for queryStrings we don't know the field names in advance. It can be anything.
  }
}


export function ChangeNaNstringToNaNnumber(elementField: any): number {
  if (elementField.toString() === 'NaN')
    return NaN;
  else
    return Number(elementField);
}

export function RemoveItemOnce(arr: number[], value: number) { // removes only the first occurrence from the array
  const index = arr.indexOf(value);
  if (index > -1)
    arr.splice(index, 1);
  return arr;
}