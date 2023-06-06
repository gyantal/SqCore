// ***ChartUltimate: VeryAdvanced (handles start/endDates, zoom, drawdown calc) ***
import * as d3 from 'd3';

// used in ChartGenerator app
export function chrtGenBacktestChrt(chartData: any, lineChrtDiv: HTMLElement, inputWidth: number, inputHeight: number, margin: any, lineChrtTooltip: HTMLElement, startDate, endDate) {
  const nameKey = chartData.map(function(d: any) { return d.name; }); // list of group names
  console.log(startDate, endDate); // need to use these dates and filter the data
  // adding colors for keys
  const color = d3.scaleOrdinal()
      .domain(nameKey)
      .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);
  // Extract all values and Dates from the priceData array
  const values = chartData.flatMap((d) => d.priceData.map((p) => p.value));
  const dates = chartData.flatMap((d) => d.priceData.map((p) => new Date(p.date)));

  // Calculate the domain for x-axis (dates)
  const xMin = d3.min(dates);
  const xMax = d3.max(dates);

  // Calculate the domain for y-axis (values)
  const yMinAxis = d3.min(values);
  const yMaxAxis = d3.max(values);
  // range of data configuring
  const scaleX = d3.scaleTime().domain([xMin as any, xMax as any]).range([0, inputWidth]);
  const scaleY = d3.scaleLinear().domain([yMinAxis as any - 5, yMaxAxis as any + 5]).range([inputHeight, 0]);

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
      .data(chartData)
      .enter()
      .append('path')
      .attr('fill', 'none')
      .attr('stroke', (d: any) => color(d.name) as any)
      .attr('stroke-width', .8)
      .attr('d', (d:any) => (d3.line()
          .x((r: any) => scaleX(r.date))
          .y((r: any) => scaleY(r.value))
          .curve(d3.curveCardinal))(d.priceData) as any);

  const legendSpace = inputWidth/chartData.length; // spacing for legend
  // Add the Legend
  backtestChrt.selectAll('rect')
      .data(chartData)
      .enter().append('text')
      .attr('x', (d: any, i: any) => ((legendSpace/2) + i * legendSpace ))
      .attr('y', 35)
      .style('fill', (d: any) => color(d.name) as any)
      .text((d: any) => (d.name));

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

  function onMouseMove(event: any) {
    const datesArray: any[] = [];
    chartData.forEach((element) => {
      element.priceData.forEach((dataPoint) => {
        datesArray.push(new Date(dataPoint.date));
      });
    });

    const xCoord = scaleX.invert(d3.pointer(event)[0]).getTime();
    const yCoord = d3.pointer(event)[1];
    const closestXCoord = datesArray.sort((a, b) => Math.abs(xCoord - a.getTime()) - Math.abs(xCoord - b.getTime()))[0];

    tooltipLine
        .attr('stroke', 'black')
        .attr('x1', scaleX(closestXCoord))
        .attr('x2', scaleX(closestXCoord))
        .attr('y1', 0 + 10)
        .attr('y2', inputHeight);

    tooltipPctChg
        .html('percent values :' + '<br>')
        .style('display', 'block')
        .style('left', event.pageX + 10 + 'px')
        .style('top', event.pageY - yCoord + 'px')
        .selectAll()
        .data(chartData)
        .enter()
        .append('div')
        .style('color', (d: any) => color(d.name) as any)
        .html((d: any) => {
          const closestYCoord = d.priceData.find((h: any) => h.date.getTime() === closestXCoord.getTime());
          return d.name + ': ' + closestYCoord.value.toFixed(2) + '%';
        });
  }
}