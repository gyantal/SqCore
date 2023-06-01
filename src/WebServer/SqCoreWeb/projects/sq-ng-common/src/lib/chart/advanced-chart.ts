import * as d3 from 'd3';

export function processUiWithPrtfRunResultChrt(chrtData: { date: Date; value: number; }[], lineChrtDiv: HTMLElement, inputWidth: number, inputHeight: number, margin: any, xMin: any, xMax: any, yMinAxis: any, yMaxAxis: any) {
  // range of data configuring
  const scaleX = d3.scaleTime().domain([xMin, xMax]).range([0, inputWidth]);
  const scaleY = d3.scaleLinear().domain([yMinAxis - 500, yMaxAxis + 500]).range([inputHeight, 0]); // as the chart values ranges are high we need a big number to subtract to define the scaleY, otherwise the chart will go below the x-axis

  const pfChrt = d3.select(lineChrtDiv).append('svg')
      .attr('width', inputWidth + margin.left + margin.right)
      .attr('height', inputHeight + margin.top + margin.bottom)
      .append('g')
      .attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

  pfChrt.append('g')
      .attr('transform', 'translate(0,' + inputHeight + ')')
      .call(d3.axisBottom(scaleX));
  pfChrt.append('g').call(d3.axisLeft(scaleY));

  // Define the line
  const line = d3.line()
      .x((r: any) => scaleX(r.date))
      .y((r: any) => scaleY(r.value))
      .curve(d3.curveCardinal);

  const chrtline = pfChrt.append('g');
  const focus = pfChrt.append('g').style('display', 'none');
  // Add the valueline path.
  chrtline.append('path')
      .attr('class', 'line')
      .datum(chrtData) // Binds data to the line
      .attr('d', line as any)
      .attr('fill', 'none')
      .attr('stroke', 'blue')
      .attr('stroke-width', 1.5);

  // append the x line
  focus.append('line')
      .attr('class', 'x')
      .style('stroke', 'blue')
      .style('stroke-dasharray', '3,3')
      .style('opacity', 0.5)
      .attr('y1', 0)
      .attr('y2', inputHeight);

  // append the y line
  focus.append('line')
      .attr('class', 'y')
      .style('stroke', 'blue')
      .style('stroke-dasharray', '3,3')
      .style('opacity', 0.5)
      .attr('x1', inputWidth)
      .attr('x2', inputWidth);

  // append the circle at the intersection
  focus.append('circle')
      .attr('class', 'y')
      .style('fill', 'none')
      .style('stroke', 'blue')
      .attr('r', 4);

  // place the value at the intersection
  focus.append('text')
      .attr('class', 'y1')
      .style('stroke', 'white')
      .style('stroke-width', '3.5px')
      .style('opacity', 0.8)
      .attr('dx', -10)
      .attr('dy', '-2em');
  focus.append('text')
      .attr('class', 'y2')
      .attr('dx', -10)
      .attr('dy', '-2em');

  // place the date at the intersection
  focus.append('text')
      .attr('class', 'y3')
      .style('stroke', 'white')
      .style('stroke-width', '3.5px')
      .style('opacity', 0.8)
      .attr('dx', -30)
      .attr('dy', '-1em');
  focus.append('text')
      .attr('class', 'y4')
      .attr('dx', -30)
      .attr('dy', '-1em');

  // append the rectangle to capture mouse
  pfChrt.append('rect')
      .attr('width', inputWidth)
      .attr('height', inputHeight)
      .style('fill', 'none')
      .style('pointer-events', 'all')
      .on('mouseover', function() { focus.style('display', null); })
      .on('mouseout', function() { focus.style('display', 'none'); })
      .on('mousemove', mousemove);

  const formatMonth = d3.timeFormat('%Y%m%dT%H:%M');
  const bisectDate = d3.bisector((r: any) => r.date).left;

  function mousemove(event: any) {
    const x0 = scaleX.invert(d3.pointer(event)[0]);
    const i = bisectDate(chrtData, x0, 1);
    const d0 = chrtData[i - 1];
    const d1 = chrtData[i];
    const r = (x0.getTime() - d0.date.getTime()) > (d1.date.getTime() - x0.getTime()) ? d1 : d0;
    focus.select('circle.y')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')');
    focus.select('text.y1')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')')
        .text(r.value);
    focus.select('text.y2')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')')
        .text(d3.format(',')(r.value));
    focus.select('text.y2')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')')
        .text(d3.format(',')((r.value)));
    focus.select('text.y3')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')')
        .text(formatMonth(r.date));
    focus.select('text.y4')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')')
        .text(formatMonth(r.date));
    focus.select('.x')
        .attr('transform', 'translate(' + scaleX(r.date) + ',' + scaleY(r.value) + ')')
        .attr('y2', inputHeight - scaleY(r.value));
    focus.select('.y')
        .attr('transform', 'translate(' + inputWidth * -1 + ',' + scaleY(r.value) + ')')
        .attr('x2', inputWidth + inputWidth);
  }
}

export function chrtGenMultiLineBacktestChrt(chrtData:{name:string, date:any, value:any}[], lineChrtDiv: HTMLElement, inputWidth: number, inputHeight: number, margin: any, xMin: any, xMax: any, yMinAxis: any, yMaxAxis: any, lineChrtTooltip: HTMLElement) {
  interface GroupedData {
    name: string;
    histPrices: { date: string; value: number }[];
  }

  // Initialize an empty array to store grouped data
  const dataGroups: GroupedData[] = [];
  for (const item of chrtData) { // Iterate over each entry in the chartData array
    const { name, date, value } = item;
    const existingEntry = dataGroups.find((r) => r.name === name); // Check if there is an existing entry with the same name in groupedData
    if (existingEntry) // If an existing entry is found, push the date and value to its priceData array
      existingEntry.histPrices.push({ date, value});
    else // If no existing entry is found, create a new entry with name and initial priceData array
      dataGroups.push({ name, histPrices: [{ date, value }] });
  }
  console.log(dataGroups);

  const nameKey = dataGroups.map(function(d: any) { return d.name; }); // list of group names

  // adding colors for keys
  const color = d3.scaleOrdinal()
      .domain(nameKey)
      .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);

  // range of data configuring
  const scaleX = d3.scaleTime().domain([xMin, xMax]).range([0, inputWidth]);
  const scaleY = d3.scaleLinear().domain([yMinAxis - 5, yMaxAxis + 5]).range([inputHeight, 0]);

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
      .data(dataGroups)
      .enter()
      .append('path')
      .attr('fill', 'none')
      .attr('stroke', (d: any) => color(d.name) as any)
      .attr('stroke-width', .8)
      .attr('d', (d:any) => (d3.line()
          .x((r: any) => scaleX(r.date))
          .y((r: any) => scaleY(r.value))
          .curve(d3.curveCardinal))(d.histPrices) as any);

  const legendSpace = inputWidth/dataGroups.length; // spacing for legend
  // Add the Legend
  backtestChrt.selectAll('rect')
      .data(dataGroups)
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
    chrtData.forEach((element) => {
      datesArray.push(element.date);
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
        .data(dataGroups)
        .enter()
        .append('div')
        .style('color', (d: any) => color(d.name) as any)
        .html((d: any) => {
          const closestYCoord = d.histPrices.find((h: any) => h.date.getTime() === closestXCoord.getTime());
          return d.name + ': ' + closestYCoord.value.toFixed(2) + '%';
        });
  }
}