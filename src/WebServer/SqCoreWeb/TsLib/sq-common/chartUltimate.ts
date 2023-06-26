// ***ChartUltimate: VeryAdvanced (handles start/endDates, zoom, drawdown calc) ***
import * as d3 from 'd3';
import { UiChartPointValue, UiChrtGenValue } from './backtestCommon';

// used in ChartGenerator app
export function chrtGenBacktestChrt(chartData: UiChrtGenValue[], lineChrtDiv: HTMLElement, inputWidth: number, inputHeight: number, margin: any, lineChrtTooltip: HTMLElement, startDate: Date, endDate: Date) {
  // Step1: slice the part of the total input data to the viewed, visualized part.
  let slicedChartData: UiChrtGenValue[] = [];
  if (startDate.getTime() === endDate.getTime()) // if the startdate and enddate are same , it means there is no need of slicing the data
    slicedChartData = chartData;
  else {
    for (const data of chartData) {
      const slicedData: UiChartPointValue[] = [];

      for (let i = 0; i < data.priceData.length; i++) {
        const chrtdata = data.priceData[i];
        const date = new Date(chrtdata.date);

        if (date >= startDate && date <= endDate)
          slicedData.push(chrtdata);
      }

      if (slicedData.length > 0) {
        const dataCopy: UiChrtGenValue = { name: data.name, date: data.date, value: data.value, chartResolution: data.chartResolution, priceData: slicedData };
        slicedChartData.push(dataCopy);
      }
    }
  }

  const nameKey: string[] = slicedChartData.map(function(d: UiChrtGenValue) { return d.name; }); // list of group names
  // adding colors for keys
  const color = d3.scaleOrdinal()
      .domain(nameKey)
      .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);

  // Extract all values and Dates from the priceData array
  const dates: Date[] = [];
  const values: number[] = [];
  for (let i = 0; i < slicedChartData.length; i++) {
    const data = slicedChartData[i];
    for (let j = 0; j < data.priceData.length; j++) {
      const point = data.priceData[j];
      dates.push(new Date(point.date));
      values.push(point.value);
    }
  }

  // Calculate the domain for x-axis (dates)
  const xMin: Date = d3.min(dates) as Date;
  const xMax: Date = d3.max(dates) as Date;

  // Calculate the domain for y-axis (values)
  const yMin: number = d3.min(values) as number;
  const yMax: number = d3.max(values) as number;
  // range of data configuring
  const scaleX = d3.scaleTime().domain([xMin, xMax]).range([0, inputWidth]);
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
      .attr('stroke', (d: UiChrtGenValue) => color(d.name) as string)
      .attr('stroke-width', .8)
      .attr('d', (d: UiChrtGenValue) => (d3.line<UiChartPointValue>()
          .x((r) => scaleX(r.date))
          .y((r) => scaleY(r.value))
          .curve(d3.curveCardinal))(d.priceData));

  const legendSpace = inputWidth/slicedChartData.length; // spacing for legend
  // Add the Legend
  backtestChrt.selectAll('rect')
      .data(slicedChartData)
      .enter().append('text')
      .attr('x', (d: UiChrtGenValue, i: any) => ((legendSpace/2) + i * legendSpace ))
      .attr('y', 35)
      .style('fill', (d: UiChrtGenValue) => color(d.name) as string)
      .text((d: UiChrtGenValue) => (d.name));

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
        .style('color', (d: UiChrtGenValue) => color(d.name) as string)
        .html((d: UiChrtGenValue) => {
          const closestPoint = d.priceData.find((h) => (h as UiChartPointValue).date.getTime() === mouseClosestXCoord.getTime()); // TODO: check if we need getTime()
          return d.name + ': ' + (closestPoint as UiChartPointValue).value.toFixed(2) + '%';
        });
  }
}