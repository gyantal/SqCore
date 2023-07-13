// ***ChartUltimate: VeryAdvanced (handles start/endDates, zoom, drawdown calc) ***
import * as d3 from 'd3';
import { UiChartPoint, CgTimeSeries } from './backtestCommon';
import { minDate, maxDate } from '../../projects/sq-ng-common/src/lib/sq-ng-common.utils_time';

type Nullable<T> = T | null;
export class UltimateChart {
  public _chartData: CgTimeSeries[] | null = null;
  public _startDate: Date = minDate;
  public _endDate: Date = maxDate;
  public _lineChrtDiv: HTMLElement | null = null;
  public _chartWidth: number = 0;
  public _chartHeight: number = 0;
  public _margin: { top: number; right: number; bottom: number; left: number; } = { top: 50, right: 10, bottom: 30, left: 10 };
  public _lineChrtTooltip: HTMLElement | null = null;

  public Init( lineChrtDiv: HTMLElement, lineChrtTooltip: HTMLElement, chartData: CgTimeSeries[], chartWidth: number, chartHeight: number, margin: { top: number; right: number; bottom: number; left: number;}): void {
    this._lineChrtDiv = lineChrtDiv;
    this._chartData = chartData;
    this._chartWidth = chartWidth;
    this._chartHeight = chartHeight;
    this._margin = margin;
    this._lineChrtTooltip = lineChrtTooltip;
  }

  public Redraw(startDate: Date, endDate: Date): void {
    this._startDate = startDate;
    this._endDate = endDate;
    const htmlElementStr = this._lineChrtDiv?.id;
    d3.selectAll(`#${htmlElementStr} > *`).remove();
    const slicedChartData: CgTimeSeries[] = [];
    if (this._chartData == null)
      return;
    for (const data of this._chartData) {
      const slicedData: UiChartPoint[] = [];
      for (let i = 0; i < data.priceData.length; i++) {
        const chrtdata = data.priceData[i];
        const date = new Date(chrtdata.date);

        if (date >= startDate && date <= endDate)
          slicedData.push(chrtdata);
      }

      if (slicedData.length > 0) {
        const newSlicedData: UiChartPoint[] = [];
        for (let i = 0; i < slicedData.length; i++) {
          const chrtPointVal = new UiChartPoint();
          chrtPointVal.date = slicedData[i].date;
          chrtPointVal.value = 100 * slicedData[i].value / slicedData[0].value;
          newSlicedData.push(chrtPointVal);
        }
        const dataCopy: CgTimeSeries = { name: data.name, chartResolution: data.chartResolution, priceData: newSlicedData };
        slicedChartData.push(dataCopy);
      }
    }

    // Extract all values from the priceData array
    const values: number[] = [];
    for (let i = 0; i < slicedChartData.length; i++) {
      const data = slicedChartData[i];
      for (let j = 0; j < data.priceData.length; j++) {
        const point = data.priceData[j];
        values.push(point.value);
      }
    }

    // Calculate the domain for y-axis (values)
    const yMin: number = d3.min(values) as number;
    const yMax: number = d3.max(values) as number;
    const nameKey: string[] = this._chartData.map(function(d: CgTimeSeries) { return d.name; }); // list of group names
    // adding colors for keys
    const color = d3.scaleOrdinal()
        .domain(nameKey)
        .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);

    // range of data configuring
    const scaleX = d3.scaleTime().domain([startDate, endDate]).range([0, this._chartWidth]);
    const scaleY = d3.scaleLinear().domain([yMin - 5, yMax + 5]).range([this._chartHeight, 0]);

    const backtestChrt = d3.select(this._lineChrtDiv).append('svg')
        .attr('width', this._chartWidth + this._margin.left + this._margin.right)
        .attr('height', this._chartHeight + this._margin.top + this._margin.bottom)
        .append('g')
        .attr('transform', 'translate(' + this._margin.left + ',' + this._margin.top + ')');

    backtestChrt.append('g')
        .attr('transform', 'translate(0,' + this._chartHeight + ')')
        .call(d3.axisBottom(scaleX));
    const chrtScaleYAxis = d3.axisLeft(scaleY).tickFormat((r: any) => Math.round(r) + '%');
    backtestChrt.append('g').call(chrtScaleYAxis);

    // Draw the line
    backtestChrt.selectAll('.line')
        .data(slicedChartData)
        .enter()
        .append('path')
        .attr('fill', 'none')
        .attr('stroke', (d: CgTimeSeries) => color(d.name) as string)
        .attr('stroke-width', .8)
        .attr('d', (d: CgTimeSeries) => (d3.line<UiChartPoint>()
            .x((r) => scaleX(r.date))
            .y((r) => scaleY(r.value))
            .curve(d3.curveCardinal))(d.priceData));

    const legendSpace = this._chartWidth/slicedChartData.length; // spacing for legend
    // Add the Legend
    backtestChrt.selectAll('rect')
        .data(slicedChartData)
        .enter().append('text')
        .attr('x', (d: CgTimeSeries, i: any) => ((legendSpace/2) + i * legendSpace ))
        .attr('y', 35)
        .style('fill', (d: CgTimeSeries) => color(d.name) as string)
        .text((d: CgTimeSeries) => (d.name));

    const tooltipPctChg = d3.select(this._lineChrtTooltip);
    const tooltipLine = backtestChrt.append('line');
    backtestChrt.append('rect')
        .attr('width', this._chartWidth as number)
        .attr('height', this._chartWidth as number)
        .attr('opacity', 0)
        .on('mousemove', onMouseMove)
        .on('mouseout', onMouseOut);

    function onMouseOut() {
      if (tooltipPctChg)
        tooltipPctChg.style('display', 'none');
      if (tooltipLine)
        tooltipLine.attr('stroke', 'none');
    }
    const chrtHeight = this._chartHeight;
    function onMouseMove(event: MouseEvent) {
      const datesArray: Date[] = [];
      slicedChartData.forEach((element) => {
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

      tooltipPctChg
          .html('percent values :' + '<br>')
          .style('display', 'block')
          .style('left', event.pageX + 10 + 'px')
          .style('top', event.pageY - yCoord + 'px')
          .selectAll()
          .data(slicedChartData)
          .enter()
          .append('div')
          .style('color', (d: CgTimeSeries) => color(d.name) as string)
          .html((d: CgTimeSeries) => {
            let closestPoint: Nullable<UiChartPoint> = null;
            let minDiff = Number.MAX_VALUE;
            for (let i = 0; i < d.priceData.length; i++) {
              const point = d.priceData[i];
              const diff = Math.abs(new Date(point.date).getTime() - mouseClosestXCoord.getTime());
              if (diff < minDiff) {
                minDiff = diff;
                closestPoint = point;
              }
            }
            if (closestPoint != null)
              return d.name + ': ' + closestPoint.value.toFixed(2) + '%';
            else
              return d.name + ': No Data';
          });
    }
  }
}