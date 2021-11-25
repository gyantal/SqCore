import { Pipe, PipeTransform } from "@angular/core";

@Pipe({
    name: 'filter'  // filterFromArray , FilterFromArrayPipe
})

export class FilterPipe implements PipeTransform {
    
    transform(items: any[], searchText: string): any[] {
        if(!items || !searchText) {
            return items;
        } 
        return items.filter( item => item.toLowerCase().indexOf(searchText.toLowerCase()) !== -1);
    }
}