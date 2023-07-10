// ***ChartUltimate: VeryAdvanced (handles start/endDates, zoom, drawdown calc) ***
import * as d3 from 'd3';
import { UiChartPointValue, CgTimeSeries } from './backtestCommon';
// import { minDate, maxDate } from '../../projects/sq-ng-common/src/lib/sq-ng-common.utils_time';

type Nullable<T> = T | null;

// used in ChartGenerator app
export function chrtGenBacktestChrt(cgTimeSeriess: CgTimeSeries[], lineChrtDiv: HTMLElement, inputWidth: number, inputHeight: number, margin: any, lineChrtTooltip: HTMLElement, startDate: Date, endDate: Date) {
  // Step1: slice the part of the total input data to the viewed, visualized part.
  const slicedChartData: CgTimeSeries[] = [];
  for (const data of cgTimeSeriess) {
    const slicedData: UiChartPointValue[] = [];

    for (let i = 0; i < data.priceData.length; i++) {
      const chrtdata = data.priceData[i];
      const date = new Date(chrtdata.date);

      if (date >= startDate && date <= endDate)
        slicedData.push(chrtdata);
    }

    if (slicedData.length > 0) {
      const newSlicedData: UiChartPointValue[] = [];
      for (let i = 0; i < slicedData.length; i++) {
        const chrtPointVal = new UiChartPointValue();
        chrtPointVal.date = slicedData[i].date;
        chrtPointVal.value = 100 * slicedData[i].value / slicedData[0].value;
        newSlicedData.push(chrtPointVal);
      }
      const dataCopy: CgTimeSeries = { name: data.name, date: data.date, value: data.value, chartResolution: data.chartResolution, priceData: newSlicedData };
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
  const nameKey: string[] = cgTimeSeriess.map(function(d: CgTimeSeries) { return d.name; }); // list of group names
  // adding colors for keys
  const color = d3.scaleOrdinal()
      .domain(nameKey)
      .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);

  // range of data configuring
  const scaleX = d3.scaleTime().domain([startDate, endDate]).range([0, inputWidth]);
  const scaleY = d3.scaleLinear().domain([yMin - 5, yMax + 5]).range([inputHeight, 0]);

  const backtestChrt = d3.select(lineChrtDiv).append('svg')
      .attr('width', inputWidth + margin.left + margin.right)
      .attr('height', inputHeight + margin.top + margin.bottom)
      .append('g')
      .attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

  backtestChrt.append('g')
      .attr('transform', 'translate(0,' + inputHeight + ')')
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
      .attr('d', (d: CgTimeSeries) => (d3.line<UiChartPointValue>()
          .x((r) => scaleX(r.date))
          .y((r) => scaleY(r.value))
          .curve(d3.curveCardinal))(d.priceData));

  const legendSpace = inputWidth/slicedChartData.length; // spacing for legend
  // Add the Legend
  backtestChrt.selectAll('rect')
      .data(slicedChartData)
      .enter().append('text')
      .attr('x', (d: CgTimeSeries, i: any) => ((legendSpace/2) + i * legendSpace ))
      .attr('y', 35)
      .style('fill', (d: CgTimeSeries) => color(d.name) as string)
      .text((d: CgTimeSeries) => (d.name));

  const tooltipPctChg = d3.select(lineChrtTooltip);
  const tooltipLine = backtestChrt.append('line');
  backtestChrt.append('rect')
      .attr('width', inputWidth)
      .attr('height', inputWidth)
      .attr('opacity', 0)
      .on('mousemove', onMouseMove)
      .on('mouseout', onMouseOut);

  function onMouseOut() {
    if (tooltipPctChg)
      tooltipPctChg.style('display', 'none');
    if (tooltipLine)
      tooltipLine.attr('stroke', 'none');
  }

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
        .attr('y2', inputHeight);

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
          let closestPoint: Nullable<UiChartPointValue> = null;
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

// class UltimateChart {
//   _htmlElementId = '';
//   _chartData: UiChrtGenValue[] | null = null;
//   _startDate: Date = minDate;
//   _endDate: Date = maxDate;

//   Init(htmlElementId: string, chartData: UiChrtGenValue[]): void {
//     this._htmlElementId = htmlElementId;
//     this._chartData = chartData;
//   }

//   Redraw(startDate: Date, endDate: Date): void {
//     this._startDate = startDate;
//     this._endDate = endDate;
//     // d3.selectAll('#pfRunResultChrt > *').remove();
//     d3.selectAll(`#${this._htmlElementId} > *`).remove();
//   }
// }