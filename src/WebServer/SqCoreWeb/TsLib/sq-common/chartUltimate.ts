// ***ChartUltimate: VeryAdvanced (handles start/endDates, zoom, drawdown calc) ***
import * as d3 from 'd3';
import { UiChartPoint, CgTimeSeries } from './backtestCommon';

type Nullable<T> = T | null;
export class UltimateChart {
  _chrtDiv: HTMLElement | null = null;
  _tooltipDiv: HTMLElement | null = null;
  _timeSeriess: CgTimeSeries[] | null = null;
  // Initialize the chart with the provided elements and time series data
  public Init(chrtDiv: HTMLElement, chrtTooltip: HTMLElement, timeSeriess: CgTimeSeries[]): void {
    this._chrtDiv = chrtDiv;
    this._tooltipDiv = chrtTooltip;
    this._timeSeriess = timeSeriess;
  }
  // Redraw the chart with updated data and dimensions
  public Redraw(startDate: Date, endDate: Date, chartWidth: number, chartHeight: number): void {
    const margin = { top: 50, right: 50, bottom: 30, left: 60 };
    const chartWidthWithMargin = chartWidth - margin.left - margin.right;
    const chartHeightWithMargin = chartHeight * 0.9 - margin.top - margin.bottom;

    // remove all _chrtDiv children. At the moment, there is only 1 child, the appended <svg>, but in the future it might be more. So, safer to for loop on all the children.
    const chrtDivChildren : HTMLCollection | null = this._chrtDiv?.children ?? null;
    if (chrtDivChildren != null) {
      for (const child of chrtDivChildren)
        this._chrtDiv?.removeChild(child);
    }
    // If there are no time series data, return without redrawing
    if (this._timeSeriess == null)
      return;

    // finding the min and max of y-axis
    let yMin: number = Number.MAX_VALUE; // Initialize with a large value
    let yMax: number = Number.MIN_VALUE; // Initialize with a small value

    for (let i = 0; i < this._timeSeriess.length; i++) {
      const data = this._timeSeriess[i];
      let firstVal: number | null = null; // To store the value of the first valid point

      for (let j = 0; j < data.priceData.length; j++) {
        const point = data.priceData[j];
        if (point.date >= startDate && point.date <= endDate) {
          if (firstVal === null)
            firstVal = point.value;

          const val = 100 * point.value / firstVal;
          if (val < yMin)
            yMin = val;

          if (val > yMax)
            yMax = val;
        }
      }
    }

    const nameKey: string[] = this._timeSeriess.map((d: CgTimeSeries) => d.name ); // Get unique group names for coloring
    const color = d3.scaleOrdinal() // Add colors for each group using d3 scaleOrdinal
        .domain(nameKey)
        .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);

    // Configure data scaling
    const scaleX = d3.scaleTime().domain([startDate, endDate]).range([0, chartWidthWithMargin]);
    const scaleY = d3.scaleLinear().domain([yMin - 5, yMax + 5]).range([chartHeightWithMargin, 0]);
    // Create an SVG container for the chart
    const backtestChrt = d3.select(this._chrtDiv).append('svg')
        .attr('width', chartWidthWithMargin + margin.left + margin.right)
        .attr('height', chartHeightWithMargin + margin.top + margin.bottom)
        .append('g')
        .attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');
    // Add X and Y axes to the chart
    backtestChrt.append('g')
        .attr('transform', 'translate(0,' + chartHeightWithMargin + ')')
        .call(d3.axisBottom(scaleX));
    const chrtScaleYAxis = d3.axisLeft(scaleY).tickFormat((r: any) => Math.round(r) + '%');
    backtestChrt.append('g').call(chrtScaleYAxis);

    // Draw the lines for each data series
    backtestChrt.selectAll('.line')
        .data(this._timeSeriess)
        .enter()
        .append('path')
        .attr('fill', 'none')
        .attr('stroke', (d: CgTimeSeries) => color(d.name) as string)
        .attr('stroke-width', .8)
        .attr('d', (d: CgTimeSeries) => { return generateSvgPath(d.priceData); });
    // Generate SVG path for each data series based on the date range and first value
    function generateSvgPath(data: UiChartPoint[]): string {
      let svgPath: string = '';
      let firstVal: number | null = null;

      for (let i = 0; i < data.length; i++) {
        const point = data[i];
        if (point.date >= startDate && point.date <= endDate) {
          if (firstVal === null) {
            firstVal = point.value;
            svgPath = 'M' + scaleX(point.date) + ',' + scaleY(100 * point.value / firstVal); // Divide the initial value by firstVal
          }
          if (i > 0) {
            const p1 = data[i - 1];
            const p2 = data[i];
            const dx = scaleX(p2.date) - scaleX(p1.date);
            const dy = scaleY(100 * p2.value / firstVal) - scaleY(100 * p1.value / firstVal); // Divide p1.value and p2.value by firstVal
            const x1 = scaleX(p1.date) + dx * 0.2;
            const y1 = scaleY(100 * p1.value / firstVal) + dy * 0.2; // Divide p1.value by firstVal
            const x2 = scaleX(p2.date) - dx * 0.2;
            const y2 = scaleY(100 * p2.value / firstVal) - dy * 0.2; // Divide p2.value by firstVal

            svgPath += `C${x1},${y1},${x2},${y2},${scaleX(p2.date)},${scaleY(100 * p2.value / firstVal)}`;
          }
        }
      }
      return svgPath;
    }

    const legendSpace = chartWidthWithMargin / this._timeSeriess.length; // Calculate spacing for legend

    backtestChrt.selectAll('rect') // Add the Legend to the chart
        .data(this._timeSeriess)
        .enter().append('text')
        .attr('x', (d: CgTimeSeries, i: any) => ((legendSpace/2) + i * legendSpace ))
        .attr('y', 35)
        .style('fill', (d: CgTimeSeries) => color(d.name) as string)
        .text((d: CgTimeSeries) => (d.name));

    // Create tooltip elements and handle mouse events
    const tooltipPctChg = d3.select(this._tooltipDiv);
    const tooltipLine = backtestChrt.append('line');
    backtestChrt.append('rect')
        .attr('width', chartWidthWithMargin as number)
        .attr('height', chartWidthWithMargin as number)
        .attr('opacity', 0)
        .on('mousemove', onMouseMove)
        .on('mouseout', onMouseOut);

    function onMouseOut() {
      if (tooltipPctChg)
        tooltipPctChg.style('display', 'none');
      if (tooltipLine)
        tooltipLine.attr('stroke', 'none');
    }

    const timeSeriess = this._timeSeriess;
    function onMouseMove(event: MouseEvent) {
      const datesArray: Date[] = [];
      timeSeriess.forEach((element) => {
        element.priceData.forEach((dataPoint) => {
          datesArray.push(new Date(dataPoint.date));
        });
      });

      const xCoord = scaleX.invert(d3.pointer(event)[0]).getTime();
      const yCoord = d3.pointer(event)[1];

      // finding the closest Xcoordinate of mouse event
      let closestDate = datesArray[0];
      let closestDiff = Math.abs(xCoord - closestDate.getTime());

      for (let i = 1; i < datesArray.length; i++) {
        const currentDate = datesArray[i];
        const currentDiff = Math.abs(xCoord - currentDate.getTime());

        if (currentDiff < closestDiff) {
          closestDate = currentDate;
          closestDiff = currentDiff;
        }
      }

      const mouseClosestXCoord: Date = closestDate;

      tooltipLine
          .attr('stroke', 'black')
          .attr('x1', scaleX(mouseClosestXCoord))
          .attr('x2', scaleX(mouseClosestXCoord))
          .attr('y1', 0 + 10)
          .attr('y2', chartHeightWithMargin as number);

      tooltipPctChg
          .html('percent values :' + '<br>')
          .style('display', 'block')
          .style('left', event.pageX + 10 + 'px')
          .style('top', event.pageY - yCoord + 'px')
          .selectAll()
          .data(timeSeriess)
          .enter()
          .append('div')
          .style('color', (d: CgTimeSeries) => color(d.name) as string)
          .html((d: CgTimeSeries) => {
            let closestPoint: Nullable<UiChartPoint> = null;
            let minDiff = Number.MAX_VALUE;
            let firstVal: number | null = null; // To store the value of the first valid point
            for (let i = 0; i < d.priceData.length; i++) {
              const point = d.priceData[i];
              if (point.date >= startDate && point.date <= endDate) {
                if (firstVal === null)
                  firstVal = point.value;

                const diff = Math.abs(new Date(point.date).getTime() - mouseClosestXCoord.getTime());
                if (diff < minDiff) {
                  minDiff = diff;
                  closestPoint = point;
                }
              }
            }
            if (closestPoint != null)
              return d.name + ': ' + (100 * closestPoint.value / firstVal!).toFixed(2) + '%';
            else
              return d.name + ': No Data';
          });
    }
  }
}