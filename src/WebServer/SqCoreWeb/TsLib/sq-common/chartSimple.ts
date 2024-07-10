// **** Used in LiveStrategies and Volatality visualizer ****
import * as d3 from 'd3';
import { UiChrtval } from './sq-globals';

export function shortMonthFormat(date: any) : string {
  const formatMillisec = d3.timeFormat('.%L');
  const formatShortMonth = d3.timeFormat('%Y-%m-%d');
  const formatYear = d3.timeFormat('%Y');
  return (d3.timeSecond(date) < date ? formatMillisec :
    d3.timeYear(date) < date ? formatShortMonth :
    formatYear)(date);
}

// requires data - raw format
// requires div element to select and append svg
// requires data lables, xLabel and yLabel
// if the tickformat is date we need to add shortformat date function
// requires another div for adding tooltip
// if x-axis is not date format use v.0 version else use v.1

// Under Development - Daya
// version v.2 - version to work for both date and number as xAxis

export function sqLineChartGenerator(noAssets: number, nCurrData: number, assetNames2Array: string[],
    assChartMtx: Array<Array<any>>, // Array of DailyData. DailyData is also an array, starting with the Date|number and followed by that daily price for all assets
    xLabel: string, yLabel: string, yScaleTickFormat: string,
    lineChrtDiv: HTMLElement, lineChrtTooltip: HTMLElement, isDrawCricles: boolean) {
  const margin = {top: 10, right: 30, bottom: 50, left: 60};
  const width = 760 - margin.left - margin.right;
  const height = 450 - margin.top - margin.bottom;

    interface pctChngStckPriceData {
    ticker: string;
    date: Date | number;
    price: number;
    }

    let isXvalueDate: boolean = true;
    if (assChartMtx.length > 0 && assChartMtx[0].length > 0 && !isNaN(assChartMtx[0][0]))
      isXvalueDate = false;

    const stckChrtData: pctChngStckPriceData[] = [];

    for (let j = 0; j < noAssets; j++) {
      for (let i = 0; i < nCurrData; i++) {
        const chrtData: pctChngStckPriceData = {
          ticker: assetNames2Array[j],
          date: isXvalueDate ? new Date(assChartMtx[i][0]) : parseFloat(assChartMtx[i][0]), // Date()
          price: parseFloat(assChartMtx[i][j + 1]),
        };
        stckChrtData.push(chrtData);
      }
    }
    let xScale;
    if (isXvalueDate) {
      const xMin = d3.min(stckChrtData, (r:{ date: any; }) => r.date as Date);
      const xMax = d3.max(stckChrtData, (r:{ date: any; }) => r.date as Date);
      // Add X axis --> it is a date format
      xScale = d3.scaleLinear().domain([xMin as Date, xMax as Date]).range([0, width]);
    } else {
      const xMin = d3.min(stckChrtData, (r:{ date: any; }) => r.date as number);
      const xMax = d3.max(stckChrtData, (r:{ date: any; }) => r.date as number);
      // Add X axis --> it is a number format
      xScale = d3.scaleLinear().domain([xMin as number, xMax as number]).range([0, width]);
    }
    const yMin = d3.min(stckChrtData, (r:{ price: any; }) => r.price as number);
    const yMax = d3.max(stckChrtData, (r:{ price: any; }) => r.price as number);

    // Add Y axis
    const yScale = d3.scaleLinear()
        .domain([(yMin as number) - ((yMax as number) - (yMin as number)) * 0.1, (yMax as number) * 1.1]) // *. increase by 10%. *1.1, $50 $200, +5, fine. 0.5 ... 0.8 + 5?
        .range([height, 0]);

    const lineChrt = d3.select(lineChrtDiv)
        .append('svg')
        .style('background', 'white')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom)
        .append('g')
        .attr('transform',
            'translate(' + margin.left + ',' + margin.top + ')');

    let chrtScaleXAxis; let chrtScaleYAxis;
    if (isXvalueDate) {
      chrtScaleXAxis = d3.axisBottom(xScale).tickSize(-height).tickFormat(shortMonthFormat);
      chrtScaleYAxis = d3.axisLeft(yScale).tickSize(-width).tickFormat((d: any) => d + yScaleTickFormat);
    } else {
      chrtScaleXAxis = d3.axisBottom(xScale).tickSize(-height);
      chrtScaleYAxis = d3.axisLeft(yScale).tickSize(-width).tickFormat((d: any) => yScaleTickFormat + d);
    }

    lineChrt.append('g')
        .attr('class', 'grid')
        .attr('transform', 'translate(0,' + height +')')
        .call(chrtScaleXAxis)
        .selectAll('text')
        .style('text-anchor', 'end')
        .attr('transform', 'rotate(-25)');

    lineChrt.append('g')
        .attr('class', 'grid')
        .call(chrtScaleYAxis);

    // x axis label
    lineChrt
        .append('text')
        .attr('class', 'pctChgLabel')
        .attr('transform', 'translate(' + width / 2 + ' ,' + (height + 42) + ')')
        .text(xLabel);

    // y axis label
    lineChrt
        .append('text')
        .attr('class', 'pctChgLabel')
        .attr('transform', 'rotate(-90)')
        .attr('y', 0 - (margin.left))
        .attr('x', 0 - (height / 2))
        .attr('dy', '2em')
        .text(yLabel);
    // grouping the data
    const stckDataGroups: any[] = [];
    stckChrtData.forEach(function(this: any, a) {
      if (!this[a.ticker]) {
        this[a.ticker] = { ticker: a.ticker, priceData: [] };
        stckDataGroups.push(this[a.ticker]);
      }
      this[a.ticker].priceData.push({ date: a.date, price: a.price });
    }, Object.create(null));
    console.log(stckDataGroups);

    const stckKey = stckDataGroups.map(function(d: any) { return d.ticker; }); // list of group names

    // adding colors for keys
    const color = d3.scaleOrdinal()
        .domain(stckKey)
        .range(['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000']);

    // Draw the line
    lineChrt.selectAll('.line')
        .data(stckDataGroups)
        .enter()
        .append('path')
        .attr('fill', 'none')
        .attr('stroke', (d: any) => color(d.ticker) as any)
        .attr('stroke-width', .8)
        .attr('d', (d:any) => (d3.line()
            .x((d: any) => xScale(d.date) as number)
            .y((d: any) => yScale(d.price) as number))(d.priceData) as any);
    if (isDrawCricles) {
      lineChrt
          .selectAll('myCircles')
          .data(stckChrtData)
          .enter()
          .append('circle')
          .style('fill', 'none')
          .attr('stroke', (d: any) => color(d.ticker) as any)
          .attr('stroke-width', 0.8)
          .attr('cx', (d: any) => xScale(d.date) as number)
          .attr('cy', (d: any) => yScale(d.price) as number)
          .attr('r', 2.5);
    }

    const legendSpace = width/stckDataGroups.length; // spacing for legend
    // // Add the Legend
    lineChrt.selectAll('rect')
        .data(stckDataGroups)
        .enter().append('text')
        .attr('x', (d: any, i: any) => ((legendSpace/2) + i * legendSpace ))
        .attr('y', 35)
        .attr('class', 'pctChgLabel') // style the legend
        .style('fill', (d: any) => color(d.ticker) as any)
        .text((d: any) => (d.ticker));

    const tooltipPctChg = d3.select(lineChrtTooltip);
    const tooltipLine = lineChrt.append('line');
    lineChrt.append('rect')
        .attr('width', width)
        .attr('height', height)
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
      const daysArray: any[] = [];
      stckChrtData.forEach((element) => {
        daysArray.push(element.date);
      });

      const xCoord = xScale.invert(d3.pointer(event)[0]);
      const yCoord = d3.pointer(event)[1];
      let closestXCoord;
      if (isXvalueDate)
        closestXCoord = daysArray.sort((a, b) => Math.abs(xCoord - a.getTime()) - Math.abs(xCoord - b.getTime()))[0];
      else
        closestXCoord = daysArray.sort((a, b) => Math.abs(xCoord - a) - Math.abs(xCoord - b))[0];

      const closestYCoord = stckDataGroups[0].priceData.find((h: any) => h.date as any === closestXCoord).price;

      tooltipLine
          .attr('stroke', 'black')
          .attr('x1', xScale(closestXCoord))
          .attr('x2', xScale(closestXCoord))
          .attr('y1', 0 + 10)
          .attr('y2', height);

      tooltipPctChg
          .html('Percentage Changes :' + '<br>')
          .style('display', 'block')
          .style('left', event.pageX + 10)
          .style('top', (event.pageY - yCoord + yScale(closestYCoord)) + 15)
          .selectAll()
          .data(stckDataGroups)
          .enter()
          .append('div')
          .style('color', (d: any) => color(d.ticker) as any)
          .html((d: any) => d.ticker + ': '+ (isXvalueDate ? ((d.priceData.find((h: any) => h.date.getTime() as any === closestXCoord.getTime() as any).price) + yScaleTickFormat) : (yScaleTickFormat + (d.priceData.find((h: any) =>h.date as any === closestXCoord as any)).price)));
    }
}

// Returns the abbreviated month name (e.g., Jan-Dec) for date ranges within a single year. We want to display the abbreviated month format on the x-axis.
export function date2MMM(date: any) : string {
  const formatMillisec = d3.timeFormat('.%L');
  const formatShortMonth = d3.timeFormat('%b');
  const formatYear = d3.timeFormat('%Y');
  // Check conditions to determine which format to use
  // If the date includes milliseconds, format with milliseconds
  // If the date is within the specific range, format with the specified format
  // Otherwise, format with the year
  return (d3.timeSecond(date) < date ? formatMillisec : d3.timeYear(date) < date ? formatShortMonth : formatYear)(date);
}

// used in 2 places: MarketDashboard/BrAccViewer and TechnicalAnalyzer
export function drawHistChartFromData(chrtData1: UiChrtval[], chrtData2: UiChrtval[] | null, lineChrtDiv: HTMLElement, inputWidth: number, inputHeight: number, margin: any, xMin: number, xMax: number, yMinAxis: number, yMaxAxis: number, yAxisTickformat: string, firstEleOfHistDataArr1: any, isNavChrt: boolean) {
  let isShowSecondaryChart: boolean = false;
  if (chrtData2 != null)
    isShowSecondaryChart = true;

  const chrt = d3.select(lineChrtDiv).append('svg')
      .attr('width', inputWidth + margin.left + margin.right)
      .attr('height', inputHeight + margin.top + margin.bottom)
      .append('g')
      .attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');

  // range of data configuring
  const chrtScaleX = d3.scaleTime().domain([xMin, xMax]).range([0, inputWidth]);
  const chrtScaleY = d3.scaleLinear().domain([yMinAxis - 5, yMaxAxis + 5]).range([inputHeight, 0]);

  chrt.append('g')
      .attr('transform', 'translate(0,' + inputHeight + ')')
      .call(d3.axisBottom(chrtScaleX).tickFormat(date2MMM));

  const chrtScaleYAxis = d3.axisLeft(chrtScaleY).tickFormat((r: any) => Math.round(r) + yAxisTickformat);
  chrt.append('g').call(chrtScaleYAxis);

  // Define the line
  const line = d3.line()
      .x((r: any) => chrtScaleX(r.date))
      .y((r: any) => chrtScaleY(r.sdaClose));

  const line2 = d3.line()
      .x((r: any) => chrtScaleX(r.date))
      .y((r: any) => chrtScaleY(r.sdaClose));

  const chrtline = chrt.append('g');
  chrtline.append('path') // Add the chrtdata to form a path.
      .attr('class', 'line')
      .style('fill', 'none')
      .style('stroke', 'blue')
      .datum(chrtData1) // Binds data to the line
      .attr('d', line as any);

  if (isShowSecondaryChart) {
    chrtline.append('path')
        .attr('class', 'line2')
        .style('fill', 'none')
        .style('stroke-dasharray', ('3, 3'))
        .datum(chrtData2) // Binds data to the line
        .attr('d', line2 as any);
  }

  const focus = chrt.append('g').style('display', 'none');
  focus.append('line') // append the x line
      .attr('class', 'x')
      .style('stroke', 'blue')
      .style('stroke-dasharray', '3,3')
      .style('opacity', 0.5)
      .attr('y1', 0)
      .attr('y2', inputHeight);

  focus.append('line') // append the y line
      .attr('class', 'y')
      .style('stroke', 'blue')
      .style('stroke-dasharray', '3,3')
      .style('opacity', 0.5)
      .attr('x1', inputWidth)
      .attr('x2', inputWidth);
  focus.append('line2')
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
  chrt.append('rect')
      .attr('width', inputWidth)
      .attr('height', inputHeight)
      .style('fill', 'none')
      .style('pointer-events', 'all')
      .on('mouseover', function() { focus.style('display', null); })
      .on('mouseout', function() { focus.style('display', 'none'); })
      .on('mousemove', mousemove);

  const formatMonth = d3.timeFormat('%Y%m%d');
  const bisectDate = d3.bisector((r: any) => r.date).left;

  function mousemove(event: any) {
    const x0: Date = chrtScaleX.invert(d3.pointer(event)[0]);
    const i: number = bisectDate(chrtData1, x0, 1);
    const d0: UiChrtval = chrtData1[i - 1];
    const d1: UiChrtval = chrtData1[i];
    const r: UiChrtval = (x0.getTime() - d0.date.getTime()) > (d1.date.getTime() - x0.getTime()) ? d1 : d0;
    focus.select('circle.y')
        .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')');
    focus.select('text.y1')
        .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')')
        .text(Math.round(r.sdaClose * firstEleOfHistDataArr1 / 100));
    if (isNavChrt) {
      focus.select('text.y2')
          .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')')
          .text(d3.format(',')(Math.round(r.sdaClose * firstEleOfHistDataArr1 / 100)) + 'K');
    } else {
      focus.select('text.y2')
          .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')')
          .text(d3.format(',')(Math.round(r.sdaClose * firstEleOfHistDataArr1 / 100)));
    }
    focus.select('text.y3')
        .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')')
        .text(formatMonth(r.date));
    focus.select('text.y4')
        .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')')
        .text(formatMonth(r.date));
    focus.select('.x')
        .attr('transform', 'translate(' + chrtScaleX(r.date) + ',' + chrtScaleY(r.sdaClose) + ')')
        .attr('y2', inputHeight - chrtScaleY(r.sdaClose));
    focus.select('.y')
        .attr('transform', 'translate(' + inputWidth * -1 + ',' + chrtScaleY(r.sdaClose) + ')')
        .attr('x2', inputWidth + inputWidth);
  }
}