// ***ChartUltimate: VeryAdvanced (handles start/endDates, zoom, drawdown calc) ***
import * as d3 from 'd3';
import { UiChartPoint, CgTimeSeries, LineStyle } from './backtestCommon';
import { SqNgCommonUtilsTime } from '../../projects/sq-ng-common/src/lib/sq-ng-common.utils_time';

type Nullable<T> = T | null;
export class UltimateChart {
  static _primaryColors = ['#0000ff', '#e41a1c', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000'];
  static _secondaryColors = ['#44ADE2', '#F94A4A', '#8DDD42', '#9F3FD3', '#FCAC4B', '#F9564A', '#67EA46', '#FFE74C', '#FFFF4C', '#3ED1B6', '#5241D8', '#B74CFF'];
  static _primaryStrokeWidth: number = 1.3;
  static _secondaryStrokeWidth: number = 1;
  static _legendSpacing = 25; // Adjust this value to control the spacing between legend items
  static _legendX = 10; // Adjust this value to control the starting X position
  static _legendY = 10; // Adjust this value to control the starting Y position
  _chrtDiv: HTMLElement | null = null;
  _tooltipDiv: HTMLElement | null = null;
  _timeSeriess: CgTimeSeries[] | null = null;
  _chartWidth: number = 0;
  _chartHeight: number = 0;
  _innerMargin: {top: number; right: number; bottom: number; left: number;} = {top: 50, right: 50, bottom: 30, left: 60}; // this is private decision to draw the chart using these 'virtual' inner margins. The chrtDiv coming from the main app has no official CSS margin at all
  // Initialize the chart with the provided elements and time series data
  public Init(chrtDiv: HTMLElement, chrtTooltip: HTMLElement, timeSeriess: CgTimeSeries[]): void {
    this._chrtDiv = chrtDiv;
    this._tooltipDiv = chrtTooltip;
    this._timeSeriess = timeSeriess;
  }
  // Redraw the chart with updated data and dimensions
  public Redraw(startDate: Date, endDate: Date, chartWidth: number, chartHeight: number): void {
    this._chartWidth = chartWidth - this._innerMargin.left - this._innerMargin.right;
    this._chartHeight = chartHeight - this._innerMargin.top - this._innerMargin.bottom;

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
    let yMinPct: number = Number.MAX_VALUE;
    let yMaxPct: number = Number.MIN_VALUE;

    for (let i = 0; i < this._timeSeriess.length; i++) {
      const timeSeries = this._timeSeriess[i];
      let firstVal: number | null = null; // To store the value of the first valid point
      let yMin: number = Number.MAX_VALUE; // Initialize with a large value
      let yMax: number = Number.MIN_VALUE; // Initialize with a small value

      for (let j = 0; j < timeSeries.priceData.length; j++) {
        const point = timeSeries.priceData[j];
        if (point.date < startDate || point.date > endDate)
          continue;

        if (firstVal == null)
          firstVal = point.value;

        if (point.value < yMin)
          yMin = point.value;

        if (point.value > yMax)
          yMax = point.value;
      }
      if (firstVal == null)
        continue;
      const yMinPctNew = 100 * yMin / firstVal;
      if (yMinPctNew < yMinPct)
        yMinPct = yMinPctNew;

      const yMaxPctNew = 100 * yMax / firstVal;
      if (yMaxPctNew > yMaxPct)
        yMaxPct = yMaxPctNew;
    }

    // Configure data scaling
    const scaleX = d3.scaleTime().domain([startDate, endDate]).range([0, this._chartWidth]);
    const scaleY = d3.scaleLinear().domain([yMinPct - 5, yMaxPct + 5]).range([this._chartHeight, 0]);
    // Create an SVG container for the chart
    console.log('backtestChrt this._chrtDiv: before', this._chrtDiv?.clientHeight);
    const backtestChrt = d3.select(this._chrtDiv).append('svg')
        .attr('width', this._chartWidth + this._innerMargin.left + this._innerMargin.right)
        .attr('height', this._chartHeight + this._innerMargin.top + this._innerMargin.bottom)
        .append('g')
        .attr('transform', 'translate(' + this._innerMargin.left + ',' + this._innerMargin.top + ')');
    console.log('backtestChrt this._chrtDiv: after', this._chrtDiv?.clientHeight);
    // Add X and Y axes to the chart
    backtestChrt.append('g')
        .attr('transform', 'translate(0,' + this._chartHeight + ')')
        .call(d3.axisBottom(scaleX));
    const chrtScaleYAxis = d3.axisLeft(scaleY);
    backtestChrt.append('g').call(chrtScaleYAxis);

    // Draw the lines for each data series
    backtestChrt.selectAll('.line')
        .data(this._timeSeriess)
        .enter()
        .append('path')
        .attr('fill', 'none')
        .attr('stroke', (d: CgTimeSeries, i: number) => getColors(d, i))
        .attr('stroke-width', (d: CgTimeSeries) => {
          if (d.isPrimary)
            return UltimateChart._primaryStrokeWidth;
          else
            return UltimateChart._secondaryStrokeWidth;
        })
        .attr('d', (d: CgTimeSeries) => { return generateSvgPath(d.priceData); })
        .attr('style', (d: CgTimeSeries) => {
          switch (d.linestyle) {
            case LineStyle.Dotted:
              return 'stroke-dasharray: 2,2'; // Set to a dotted line
            case LineStyle.Dashed:
              return 'stroke-dasharray: 5,5'; // Set to a dashed line
            case LineStyle.DashDot:
              return 'stroke-dasharray: 5,2,1,2'; // Set to a dash-dot line
            default:
              return 'stroke-dasharray: none'; // Default to a solid line
          }
        });

    backtestChrt.selectAll('rect') // Add the Legend to the chart
        .data(this._timeSeriess)
        .enter().append('text')
        .attr('x', UltimateChart._legendX)
        .attr('y', (d: CgTimeSeries, i: any) => ( UltimateChart._legendY + i * UltimateChart._legendSpacing ))
        .attr('style', (d: CgTimeSeries) => getStyle(d))
        .style('fill', (d: CgTimeSeries, i: number) => getColors(d, i))
        .text((d: CgTimeSeries) => (d.name));

    backtestChrt.append('rect')
        .attr('width', this._chartWidth as number)
        .attr('height', this._chartHeight as number)
        .attr('opacity', 0)
        .on('mouseover', onMouseover)
        .on('mousemove', onMousemove)
        .on('mouseout', onMouseout);

    // Create tooltip elements and handle mouse events
    const tooltipPctChg = d3.select(this._tooltipDiv);
    const tooltipLine = backtestChrt.append('line');
    const timeSeriess = this._timeSeriess;
    const chrtHeight = this._chartHeight;

    function onMouseover() { // This function is necessary because, if the mouse is inside the chart area but not moving, the tooltip will not be visible. To address this, we need to use the mouseover event.
      tooltipPctChg.style('visibility', 'visible');
      tooltipLine.attr('stroke', 'black');
    }

    function onMousemove(event: MouseEvent) {
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
          .attr('y2', chrtHeight as number);

      const isMouseOnLeftSide: boolean = event.pageX < (chartWidth / 2); // Determine if the mouse is in the left or right half of the chart
      let tooltipPosX: number;
      if (isMouseOnLeftSide)
        tooltipPosX = event.pageX + 10;
      else {
        const tooltipElement: HTMLElement | null = tooltipPctChg.node(); // Get the tooltip element
        if (tooltipElement != null) // Ensure the tooltip is visible/rendered before calculating its width
          tooltipPosX = event.pageX - 10 - tooltipElement.offsetWidth; // offsetWidth => width of the tooltip element
        else
          tooltipPosX = 0; // defaulting to 0, if tooltipElement is null
      }

      const tooltipPosY: number = event.pageY - yCoord; // Calculate the Y position of the tooltip based on the mouse position
      let closestPoint: Nullable<UiChartPoint> = null;
      tooltipPctChg
          .style('visibility', 'visible')
          .style('left', `${tooltipPosX}px`)
          .style('top', `${tooltipPosY}px`)
          .html('percent values :' + '<br>')
          .selectAll()
          .data(timeSeriess)
          .enter()
          .append('div')
          .attr('style', (d: CgTimeSeries) => getStyle(d))
          .style('color', (d: CgTimeSeries, i: number) => getColors(d, i))
          .html((d: CgTimeSeries) => {
            let minDiff = Number.MAX_VALUE;
            let firstVal: number | null = null; // To store the value of the first valid point
            for (let i = 0; i < d.priceData.length; i++) {
              const point = d.priceData[i];
              if (point.date < startDate || point.date > endDate)
                continue;
              if (firstVal === null)
                firstVal = point.value;

              const diff = Math.abs(point.date.getTime() - mouseClosestXCoord.getTime());
              if (diff < minDiff) {
                minDiff = diff;
                closestPoint = point;
              }
            }
            if (closestPoint != null)
              return `${d.name}: ${(100 * closestPoint.value / firstVal!).toFixed(2)}%`;
            else
              return `${d.name}: No Data`;
          });

      tooltipPctChg.append('text').text('Date: ' + SqNgCommonUtilsTime.Date2PaddedIsoStr(closestPoint!.date));
    }

    function onMouseout() {
      tooltipPctChg.style('visibility', 'hidden');
      tooltipLine.attr('stroke', 'none');
    }

    // Generate SVG path for each data series based on the date range and first value
    function generateSvgPath(data: UiChartPoint[]): string {
      let svgPath: string = '';
      let firstVal: number | null = null;
      let prevPoint: UiChartPoint | null = null;

      // Adjust the startDate to midnight local time
      // When the user inputs the startDate it includes time portion.
      // e.g, if the user provides '2012-05-09', it is interpreted as Wed May 09 2012 05:30:00 GMT+0530 (local timezone).
      // resetting the time portion to local midnight.
      startDate.setHours(0, 0, 0, 0);
      for (let i = 0; i < data.length; i++) {
        const point = data[i];

        // Skip points that fall outside the specified range [startDate, endDate]
        // When using the condition `point.date < startDate`,
        // e.g, If point.date = Wed May 09 2012 00:00:00 GMT+0530 and startDate = Wed May 09 2012 05:30:00 GMT+0530 (due to timezone offset),
        // this condition will skip the current point.date and consider the next one, e.g., point.date = Thu May 10 2012 00:00:00 GMT+0530, as the starting point for plotting.
        // which is causing the plot to start from Thu May 10 2012 00:00:00 GMT+0530 and showing the 100% value on the tooltip
        if (point.date < startDate || point.date > endDate)
          continue;

        if (firstVal == null) {
          firstVal = point.value;
          svgPath = 'M' + scaleX(point.date) + ',' + scaleY(100 * point.value / firstVal);
        }

        if (prevPoint != null) {
          const dx = scaleX(point.date) - scaleX(prevPoint.date);
          const dy = scaleY(100 * point.value / firstVal) - scaleY(100 * prevPoint.value / firstVal);
          const x1 = scaleX(prevPoint.date) + dx * 0.2;
          const y1 = scaleY(100 * prevPoint.value / firstVal) + dy * 0.2;
          const x2 = scaleX(point.date) - dx * 0.2;
          const y2 = scaleY(100 * point.value / firstVal) - dy * 0.2;

          svgPath += `C${x1},${y1},${x2},${y2},${scaleX(point.date)},${scaleY(100 * point.value / firstVal)}`;
        }
        prevPoint = point; // Update prevPoint for the next iteration
      }
      return svgPath;
    }

    // get colors for primary and secondary items
    function getColors(d: CgTimeSeries, i: number): string {
      if (d.isPrimary)
        return UltimateChart._primaryColors[i % UltimateChart._primaryColors.length];
      else
        return UltimateChart._secondaryColors[i % UltimateChart._secondaryColors.length];
    }

    // get Style for primary and secondary items
    function getStyle(d: CgTimeSeries): string {
      if (d.isPrimary)
        return 'font-weight: bold;';
      else
        return 'font-style: italic;';
    }
  }
}