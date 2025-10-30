
// Usage example.
// // Sample data
// const chartData: ChartDataPoint[] = [
//   { date: new Date('2025-01-01'), value: 100 },
//   { date: new Date('2025-02-01'), value: 150 },
//   { date: new Date('2025-03-01'), value: 200 },
// ];

import { maxDate, minDate } from '../../projects/sq-ng-common/src/lib/sq-ng-common.utils_time';
import { UiChartPoint } from './backtestCommon';

// // Get the chart container
// const chartDiv = document.getElementById('chart-container') as HTMLElement;

// // Create and initialize the chart
// const chart = new Chart();
// chart.init(chartDiv, 400, 200);

// // Add a data series
// chart.addLine(chartData);

// // Set viewport to show data between two dates
// const startDate = new Date('2025-01-01');
// const endDate = new Date('2025-02-01');
// chart.setViewport(startDate, endDate);

// 1. Index-based filtering breaks when datasets start at different times.
// e.g., index 0 in dataset1 (2013–2018, 6000 pts) ≠ index 0 in dataset2 (2015–2017, 2000 pts).
// 2. Always compute the overall min/max dates across all datasets, since setViewport(start, end) may span ranges that cover multiple datasets.

// Chart line class to manage individual data series
export class ChartLine {
  private dataSet: UiChartPoint[];
  public visibleDataStartIdx: number;
  public visibleDataEndIdx: number;
  public color: string | null;
  public chartType: string | null; // line, candle, bar...
  public visBaseRefValue: number = NaN; // The first visible data point’s value, used as the base reference when converting visible data into percentage values
  public dataSetVisible: UiChartPoint[];

  constructor(dataSet: UiChartPoint[] = [], color: string | null = null, chartType: string | null = null) {
    this.dataSet = dataSet;
    this.color = color;
    this.chartType = chartType;
    this.visibleDataStartIdx = 0;
    this.visibleDataEndIdx = dataSet.length > 0 ? dataSet.length - 1 : 0;
    this.visBaseRefValue = dataSet[0]?.value ?? NaN;
    this.dataSetVisible = this.dataSet.slice(this.visibleDataStartIdx, this.visibleDataEndIdx + 1);
  }

  // Getters
  public getDataSet(): UiChartPoint[] {
    return this.dataSet;
  }

  // Setters
  public setDataSet(dataSet: UiChartPoint[]): void {
    this.dataSet = dataSet;
    this.resetVisibleIndices();
  }

  public setVisibleRange(startIdx: number, endIdx: number): void {
    if (startIdx >= 0 && this.dataSet.length > 0 && endIdx < this.dataSet.length && startIdx <= endIdx) {
      this.visibleDataStartIdx = startIdx;
      this.visibleDataEndIdx = endIdx;
      this.visBaseRefValue = !isNaN(this.dataSet[this.visibleDataStartIdx].value) ? this.dataSet[this.visibleDataStartIdx].value : NaN;
      this.dataSetVisible = this.dataSet.slice(this.visibleDataStartIdx, this.visibleDataEndIdx + 1);
    }
  }

  private resetVisibleIndices(): void {
    this.visibleDataStartIdx = 0;
    this.visibleDataEndIdx = this.dataSet.length > 0 ? this.dataSet.length - 1 : 0;
  }

  public setChartType(chartType: string): void {
    this.chartType = chartType;
  }

  public generateRandomOhlc(): void {
    for (let i = 1; i < this.dataSet.length; i++) {
      const prev: UiChartPoint = this.dataSet[i - 1];
      const curr: UiChartPoint = this.dataSet[i];

      // Generate random factor [-1, 1]
      const randomFactor: number = Math.random() * 2 - 1;
      const priceDelta: number = curr.value - prev.value;

      // Synthetic OHLC
      curr.open = curr.value - priceDelta * randomFactor;
      curr.high = Math.max(curr.value, prev.value) + Math.abs((priceDelta / 2) * randomFactor);
      curr.low = Math.min(curr.value, prev.value) - Math.abs((priceDelta / 2) * randomFactor);
      curr.close = curr.value;
    }
  }
}

class XAxis {
  public minTime: number = 0;
  public maxTime: number = 1;
  public canvasWidth: number = 1;
  public allDates: Date[] = [];

  public generateTicks(chartLines: ChartLine[]): { label: string, dataIndex: number }[] {
    if (chartLines.length == 0)
      return [];

    // Collect unique dates using an object
    const datesObj: { [key: string]: Date } = {};
    for (const chartLine of chartLines) {
      const visibleData: UiChartPoint[] = chartLine.dataSetVisible;
      for (const point of visibleData) {
        const dateKey: string = point.date.toISOString().split('T')[0];
        datesObj[dateKey] = point.date;
      }
    }

    this.allDates = Object.values(datesObj).sort((a, b) => a.getTime() - b.getTime()); // Convert to array of Date objects and sort
    if (this.allDates.length == 0)
      return [];
    this.minTime = this.allDates[0].getTime();
    this.maxTime = this.allDates[this.allDates.length - 1].getTime();
    const ticks: { label: string, dataIndex: number }[] = [];

    const visibleRangeDays: number = ((this.maxTime - this.minTime) / (1000 * 60 * 60 * 24)) + 1; // Include both start and end days in the visible range (e.g., if the difference is 1 day, display both days)
    // Add ticks at month/year boundaries
    let prevMonth: number = -1;
    let prevYear: number = -1;
    for (let i = 0; i < this.allDates.length; i++) {
      const dataPoint = this.allDates[i];
      const day: number = dataPoint.getDate();
      const month: number = dataPoint.getMonth();
      const year: number = dataPoint.getFullYear();

      if (visibleRangeDays <= 60) {
        const label: string = (month != prevMonth) ? dataPoint.toLocaleDateString('en-US', { month: 'short' }) : day.toString(); // Display the month name at the start of each month; use day number for other dates
        ticks.push({ label: label, dataIndex: i });
        prevMonth = month;
      } else if (visibleRangeDays >= 1825) { // Display the year, if the user select the visible range >= to 5 years
        if (year !== prevYear) {
          const label: string = year.toString(); // Show only the year
          ticks.push({ label: label, dataIndex: i });
          prevYear = year;
        }
      } else {
        if (month != prevMonth || year != prevYear) {
          const label: string = month === 0 ? year.toString() : dataPoint.toLocaleDateString('en-US', { month: 'short' }); // Year label if January, else month label (Feb, Mar, etc).
          ticks.push({ label: label, dataIndex: i });
          prevMonth = month;
          prevYear = year;
        }
      }
    }

    return ticks;
  }

  public render(ctx: CanvasRenderingContext2D, canvasWidth: number, chartLines: ChartLine[]): void {
    if (chartLines.length == 0)
      return;

    ctx.beginPath();
    // Draw axis line (top of canvas)
    ctx.moveTo(0, 0);
    ctx.lineTo(canvasWidth, 0);
    ctx.stroke();

    this.canvasWidth = canvasWidth;

    // Draw ticks and labels
    const ticks: { label: string, dataIndex: number }[] = this.generateTicks(chartLines);
    const visibleCount = this.allDates.length > 1 ? this.allDates.length : 1;
    const slotWidth = canvasWidth / visibleCount; // allocate equal space for each data point
    for (const tick of ticks) {
      const x: number = (tick.dataIndex + 0.5) * slotWidth; // Place tick at the center of its allocated slot
      // Draw tick mark
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, 5);
      ctx.stroke();

      // Draw tick label
      const textWidth: number = ctx.measureText(tick.label).width;
      ctx.fillStyle = 'black';
      ctx.fillText(tick.label, x - textWidth / 2, 15);
    }
  }

  public scaleXToTime(xPixel: number): Date { // Converts a horizontal pixel to data index first, then to time
    const dataIndex = Math.floor((xPixel / this.canvasWidth) * (this.allDates.length));
    const clampedIndex = Math.max(0, Math.min(dataIndex, this.allDates.length)); // Ensures the index never goes out of range
    return this.allDates[clampedIndex];
  }
}

class YAxis {
  public minValue: number = Number.MAX_VALUE; // Initialize with a large value;
  public maxValue: number = Number.MIN_VALUE; // Initialize with a small value
  public canvasHeight: number = 1;

  public generateTicks(chartLines: ChartLine[]): number[] {
    if (chartLines.length == 0)
      return [];
    // Reset
    this.minValue = Number.MAX_VALUE;
    this.maxValue = -Number.MAX_VALUE;

    // Find min and max value
    for (const chartLine of chartLines) {
      const dataset: UiChartPoint[] = chartLine.dataSetVisible;
      const firstValue: number = chartLine.visBaseRefValue;
      if (firstValue == 0 || isNaN(firstValue))
        continue; // avoid divide by zero or invalid first value

      for (const point of dataset) {
        const normalizedVal: number = (point.value / firstValue) * 100;
        if (normalizedVal < this.minValue)
          this.minValue = normalizedVal;
        if (normalizedVal > this.maxValue)
          this.maxValue = normalizedVal;
      }
    }

    if (this.minValue == Number.MAX_VALUE || this.maxValue == -Number.MAX_VALUE)
      return [];

    const valueRange: number = this.maxValue - this.minValue;
    // Determine an appropriate step size (similar to D3's "nice" ticks https://github.com/d3/d3-scale/blob/main/src/linear.js)
    const targetTickCount: number = 10; // default number of ticks
    const rawStep: number = valueRange / targetTickCount; // Calculate an initial raw step between ticks
    const stepMagnitude: number = Math.pow(10, Math.floor(Math.log10(rawStep))); // Determine the order of magnitude of the step
    const residual: number = rawStep / stepMagnitude; // Residual helps us decide a "nice" rounded step

    let niceStep: number;
    if (residual >= 5)
      niceStep = 10 * stepMagnitude;
    else if (residual >= 2)
      niceStep = 5 * stepMagnitude;
    else if (residual >= 1)
      niceStep = 2 * stepMagnitude;
    else
      niceStep = 1 * stepMagnitude;

    // Extend the domain to nice round numbers
    const roundedMin: number = Math.floor(this.minValue / niceStep) * niceStep;
    const roundedMax: number = Math.ceil(this.maxValue / niceStep) * niceStep;

    // Generate ticks from roundedMin to roundedMax
    const ticks: number[] = [];
    for (let tick = roundedMin; tick <= roundedMax; tick += niceStep)
      ticks.push(tick);

    return ticks;
  }

  public render(ctx: CanvasRenderingContext2D, canvasHeight: number, chartLines: ChartLine[]): void {
    if (chartLines.length == 0)
      return;

    const tickLabelXOffset = 10; // Controls the horizontal position of the tick labels relative to the axis.
    const tickLabelYOffset = 4; // Adjusts the vertical alignment of the tick labels to ensure they are visually centered relative to the tick marks.
    const tickMarkLength = 5; // Length of tick marks on the axis
    // Draw axis line (left side of canvas)
    ctx.moveTo(0, 0);
    ctx.lineTo(0, canvasHeight);
    ctx.stroke();
    // Draw ticks, labels
    const ticks: number[] = this.generateTicks(chartLines);

    // Add 10% padding to min and max to prevent ticks at canvas edges
    const range: number = this.maxValue - this.minValue;
    const padding: number = range * 0.01;
    this.minValue -= padding;
    this.maxValue += padding;
    this.canvasHeight = canvasHeight;
    const yScale = this.maxValue > this.minValue ? canvasHeight / (this.maxValue - this.minValue) : 1;

    for (let i = 0; i < ticks.length; i++) {
      const tick: number = ticks[i];
      const y: number = canvasHeight - (tick - this.minValue) * yScale;

      // Draw tick mark
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(tickMarkLength, y);
      ctx.stroke();

      // Draw tick label
      const label = tick.toFixed(0);
      ctx.fillStyle = 'black';
      ctx.fillText(label, tickLabelXOffset, y + tickLabelYOffset);
    }
  }

  public scaleYToValue(yPixel: number): number { // Converts a vertical pixel coordinate on the canvas to a data value(price).
    return this.maxValue - (yPixel / this.canvasHeight) * (this.maxValue - this.minValue);
  }
}

// Main chart class
export class SqChart {
  private chartLines: ChartLine[] = [];
  private chartDiv: HTMLElement | null = null;
  private canvas: HTMLCanvasElement | null = null;
  private canvasWidthPercent = 0.97;
  private canvasHeightPercent = 0.9;
  private xAxis: XAxis = new XAxis();
  private yAxis: YAxis = new YAxis();

  // Drag state
  private isDragging = false;
  private viewportStartDate: Date | null = null;
  private viewportEndDate: Date | null = null;
  private overallMinDate: Date = maxDate;
  private overallMaxDate: Date = minDate;

  public init(chartDiv: HTMLElement): void {
    this.chartDiv = chartDiv;
    const chartDivWidth = this.chartDiv.clientWidth as number;
    const chartDivHeight = this.chartDiv.clientHeight as number;
    const canvas = document.createElement('canvas');
    // Allocate space for X and Y axes by adjusting the canvas size
    canvas.width = chartDivWidth * this.canvasWidthPercent; // 97% of the ChartDiv Width
    canvas.height = chartDivHeight * this.canvasHeightPercent; // 90% of the ChartDiv Height

    this.chartDiv.appendChild(canvas);
    this.canvas = canvas;
    this.redraw();
    // ResizeObserver - Ensures the canvas stays correctly sized when the chart container is resized by user or layout change. see https://developer.mozilla.org/en-US/docs/Web/API/ResizeObserver
    new ResizeObserver(() => { this.resizeCanvasToContainer(); }).observe(this.chartDiv);

    this.enableXAxisDrag();
    this.enableXAxisZoom();
  }

  private getColor(index: number): string {
    const colors: string[] = ['#0000ff', '#e41a1c', '#4daf4a', '#984ea3', '#ff7f00', '#f781bf', '#808000', '#008000', '#a65628', '#333397', '#800080', '#000000'];
    return colors[index % colors.length];
  }

  public addLine(chartLine: ChartLine): void {
    if (chartLine.color == null)
      chartLine.color = this.getColor(this.chartLines.length);
    const dataSet: UiChartPoint[] = chartLine.getDataSet();


    this.chartLines.push(chartLine);
    // update global min/max dates
    if (dataSet.length > 0) {
      const firstDate: Date = dataSet[0].date;
      const lastDate: Date = dataSet[dataSet.length - 1].date;

      if (firstDate < this.overallMinDate)
        this.overallMinDate = firstDate;

      if (lastDate > this.overallMaxDate)
        this.overallMaxDate = lastDate;
    }
    // initialize viewport on first dataset
    if (this.viewportStartDate == null || this.viewportEndDate == null) {
      this.viewportStartDate = this.overallMinDate;
      this.viewportEndDate = this.overallMaxDate;
    }
    this.redraw();
  }

  public setChartTypeToAllChartLines(chartType: string): void {
    for (let i = 0; i < this.chartLines.length; i++)
      this.chartLines[i].setChartType(chartType);

    this.redraw(); // update the chartStyle
  }

  public setViewport(startDate: Date, endDate: Date): void {
    this.viewportStartDate = startDate;
    this.viewportEndDate = endDate;
    for (const chartLine of this.chartLines) {
      const dataSet: UiChartPoint[] = chartLine.getDataSet();
      let startIdx: number = 0;
      let endIdx: number = dataSet.length - 1;

      for (let j = 0; j < dataSet.length; j++) {
        if (dataSet[j].date >= startDate) {
          startIdx = j;
          break;
        }
      }

      for (let k = dataSet.length - 1; k >= 0; k--) {
        if (dataSet[k].date <= endDate) {
          endIdx = k;
          break;
        }
      }

      chartLine.setVisibleRange(startIdx, endIdx); // for each dataset the start and end index may or maynot be same
    }
    this.redraw();
  }

  public redraw(): void {
    if (this.canvas == null)
      return;

    const canvasRenderingCtx: CanvasRenderingContext2D | null = this.canvas.getContext('2d');
    if (canvasRenderingCtx == null)
      return;

    const canvasWidth: number = this.canvas.width;
    const canvasHeight: number = this.canvas.height;

    // Clear
    canvasRenderingCtx.clearRect(0, 0, canvasWidth, canvasHeight);

    if (this.chartLines.length == 0)
      return;

    // Update X-axis min, max based on viewport
    this.xAxis.minTime = this.viewportStartDate?.getTime() ?? this.overallMinDate.getTime();
    this.xAxis.maxTime = this.viewportEndDate?.getTime() ?? this.overallMaxDate.getTime();

    const xScale = canvasWidth / (this.xAxis.maxTime - this.xAxis.minTime);
    const yScale = canvasHeight / (this.yAxis.maxValue - this.yAxis.minValue);
    for (const chartLine of this.chartLines) {
      switch (chartLine.chartType) {
        case 'basicCandle': // A simple candle bar
          this.drawBasicCandle(chartLine, canvasRenderingCtx, xScale, yScale, canvasHeight);
          break;
        case 'candleStick': // A full candlestick showing High–Low (wick) and Open–Close (body)
          const visibleData: UiChartPoint[] = chartLine.dataSetVisible;
          // If it's a candlestick chart and OHLC values are not present, generate random data
          if ( visibleData.length > 0) {
            const chrtPoint: UiChartPoint = visibleData[0];
            const hasValidOhlcData: boolean = !isNaN(chrtPoint.open) && !isNaN(chrtPoint.high) && !isNaN(chrtPoint.low) && !isNaN(chrtPoint.close);

            if (!hasValidOhlcData)
              chartLine.generateRandomOhlc();
          }
          this.drawCandleStick(chartLine, canvasRenderingCtx, xScale, yScale, canvasHeight);
          break;
        case 'line':
          this.drawLine(chartLine, canvasRenderingCtx, xScale, yScale, canvasHeight);
          break;
        // future: area, scatter, etc.
      }
    }

    this.drawAxes(this.chartLines);

    this.displayTooltip(this.chartDiv!, canvasWidth, canvasHeight, this.xAxis, this.yAxis);

    // displaying the chart dimesions for debugging
    if (this.chartLines != null) {
      const x0: string = this.chartLines[0].dataSetVisible[0].date.toDateString();
      const y0: number = this.chartLines[0].dataSetVisible[0].value;
      const text: string = `x0: ${x0}, y0: ${y0}, width: ${canvasWidth}, height: ${canvasHeight}`;

      canvasRenderingCtx.font = '14px sans-serif';
      canvasRenderingCtx.fillStyle = '#000000';
      const textWidth: number = canvasRenderingCtx.measureText(text).width;
      canvasRenderingCtx.fillText( text, (canvasWidth - textWidth) / 2, canvasHeight / 2 );
    }
  }

  // we need to match the canvas dimensions to the container(ChartDiv), when the user resizes either by manually or window.
  public resizeCanvasToContainer(): void {
    if (this.chartDiv == null || this.canvas == null)
      return;

    const chartDivRect: DOMRect = this.chartDiv.getBoundingClientRect();
    this.canvas.width = chartDivRect.width * this.canvasWidthPercent;
    this.canvas.height = chartDivRect.height * this.canvasHeightPercent;

    this.redraw(); // Redraw chart with new canvas size
  }

  private drawAxes(chartLines: ChartLine[] | null): void {
    if (chartLines == null || this.chartDiv == null || this.canvas == null)
      return;

    // Remove old xAxisCanvas
    const existingXAxis: Element | null = this.chartDiv.querySelector('#xAxisCanvas');
    if (existingXAxis != null)
      this.chartDiv.removeChild(existingXAxis);

    // Create the xAxis canvas
    const xAxisCanvas: HTMLCanvasElement = document.createElement('canvas');
    xAxisCanvas.id = 'xAxisCanvas';
    xAxisCanvas.width = this.canvas.width;
    xAxisCanvas.height = this.canvas.height * (1 - this.canvasHeightPercent);
    xAxisCanvas.style.position = 'absolute';
    xAxisCanvas.style.top = `${this.canvas.height}px`;
    xAxisCanvas.style.left = '0px'; // align with chart

    // Append the xAxis canvas
    this.chartDiv.appendChild(xAxisCanvas);
    const xAxisCtx: CanvasRenderingContext2D | null = xAxisCanvas.getContext('2d');
    if (xAxisCtx == null)
      return;

    this.xAxis.render(xAxisCtx, this.canvas.width, chartLines);

    // Remove old yAxisCanvas
    const existingYAxis: Element | null = this.chartDiv.querySelector('#yAxisCanvas');
    if (existingYAxis != null)
      this.chartDiv.removeChild(existingYAxis);
    // Create the yAxis canvas
    const yAxisCanvas: HTMLCanvasElement = document.createElement('canvas');
    yAxisCanvas.id = 'yAxisCanvas';
    yAxisCanvas.width = this.canvas.width * (1 - this.canvasWidthPercent);
    yAxisCanvas.height = this.canvas.height;
    yAxisCanvas.style.position = 'absolute';
    yAxisCanvas.style.left = `${this.canvas.width}px`; // chart starts at margin.left
    // Append the yAxis canvas
    this.chartDiv.appendChild(yAxisCanvas);
    const yAxisCtx: CanvasRenderingContext2D | null = yAxisCanvas.getContext('2d');
    if (yAxisCtx == null)
      return;

    this.yAxis.render(yAxisCtx, this.canvas.height, chartLines);
  }

  private displayTooltip(chartDiv: HTMLElement, canvasWidth: number, canvasHeight: number, xAxis: XAxis, yAxis: YAxis) {
    // Remove old tooltipCanvas
    const existingTooltipCanvas: Element | null = chartDiv.querySelector('#tooltipCanvas');
    if (existingTooltipCanvas != null)
      chartDiv.removeChild(existingTooltipCanvas);
    // create tooltip canvas
    const tooltipCanvas = document.createElement('canvas');
    tooltipCanvas.id = 'tooltipCanvas';
    tooltipCanvas.style.position = 'absolute';
    tooltipCanvas.style.left = '0';
    tooltipCanvas.style.top = '0';
    tooltipCanvas.style.zIndex = '1';
    tooltipCanvas.style.pointerEvents = 'none';

    chartDiv.appendChild(tooltipCanvas);
    tooltipCanvas.width = canvasWidth;
    tooltipCanvas.height = canvasHeight;
    const canvasRenderingCtx = tooltipCanvas.getContext('2d');
    if (canvasRenderingCtx == null)
      return;

    const self = this; // for access inside event handler
    chartDiv.addEventListener('mousemove', function(event) {
      canvasRenderingCtx.clearRect(0, 0, canvasWidth, canvasHeight); // Clear canvas

      const chartDivRect: DOMRect = chartDiv.getBoundingClientRect();
      const mouseX: number = event.clientX - chartDivRect.left;
      const mouseY: number = event.clientY - chartDivRect.top;

      // Compute values
      const date: Date = xAxis.scaleXToTime(mouseX);
      const dateStr: string = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
      const priceStr: string = yAxis.scaleYToValue(mouseY).toFixed(2);
      // Draw vertical line
      canvasRenderingCtx.beginPath();
      canvasRenderingCtx.moveTo(mouseX, 0);
      canvasRenderingCtx.lineTo(mouseX, canvasHeight);
      canvasRenderingCtx.strokeStyle = 'green';
      canvasRenderingCtx.setLineDash([5, 5]);
      canvasRenderingCtx.stroke();

      // Draw horizontal line
      canvasRenderingCtx.beginPath();
      canvasRenderingCtx.moveTo(0, mouseY);
      canvasRenderingCtx.lineTo(canvasWidth, mouseY);
      canvasRenderingCtx.strokeStyle = 'blue';
      canvasRenderingCtx.stroke();

      // Draw price on right side (Y axis)
      const paddingX: number = 4;
      const paddingY: number = 2;
      const fontSize: number = 12;
      canvasRenderingCtx.font = `${fontSize}px sans-serif`;
      const priceTextWidth: number = canvasRenderingCtx.measureText(priceStr).width;
      const priceTextHeight: number = fontSize;

      const priceBoxX: number = canvasWidth - priceTextWidth - paddingX * 2;
      const priceBoxY: number = mouseY - priceTextHeight / 2 - paddingY;

      canvasRenderingCtx.fillStyle = 'black';
      canvasRenderingCtx.fillRect(priceBoxX, priceBoxY, priceTextWidth + paddingX * 2, priceTextHeight + paddingY * 2);

      canvasRenderingCtx.fillStyle = 'white';
      canvasRenderingCtx.fillText(priceStr, priceBoxX + paddingX, priceBoxY + priceTextHeight + paddingY / 2);

      // Draw date on bottom (X axis)
      const dateTextWidth: number = canvasRenderingCtx.measureText(dateStr).width;
      const dateTextHeight: number = fontSize;

      const dateBoxX: number = mouseX - dateTextWidth / 2 - paddingX;
      const dateBoxY: number = canvasHeight - dateTextHeight - paddingY * 2;

      canvasRenderingCtx.fillStyle = 'black';
      canvasRenderingCtx.fillRect(dateBoxX, dateBoxY, dateTextWidth + paddingX * 2, dateTextHeight + paddingY * 2);

      canvasRenderingCtx.fillStyle = 'white';
      canvasRenderingCtx.fillText(dateStr, dateBoxX + paddingX, dateBoxY + dateTextHeight + paddingY / 2);

      // Get index of hovered date from xAxis
      const hoverIndex: number = Math.floor((mouseX / xAxis.canvasWidth) * (xAxis.allDates.length));
      const hoveredPoints: { point: UiChartPoint; chartType: string }[] = [];
      // Collect hovered points from all chart lines
      for (const chartLine of self.chartLines) {
        const visibleData: UiChartPoint[] = chartLine.dataSetVisible;
        const chartType: string = chartLine.chartType!;

        if (hoverIndex < visibleData.length) {
          const point = visibleData[hoverIndex];
          hoveredPoints.push({ point, chartType });
        }
      }

      // Draw OHLC or value info for all hovered points (stacked)
      if (hoveredPoints.length > 0) {
        const fontSize: number = 13;
        const startX: number = 10;
        let currentY: number = 16;

        canvasRenderingCtx.font = `${fontSize}px sans-serif`;
        canvasRenderingCtx.textAlign = 'left';
        canvasRenderingCtx.fillStyle = 'black';

        for (let i = 0; i < hoveredPoints.length; i++) {
          const { point, chartType } = hoveredPoints[i];
          let hoveredValue: string = '';

          if (chartType == 'candleStick') {
            const open: string = point.open?.toFixed(2) ?? '-';
            const high: string = point.high?.toFixed(2) ?? '-';
            const low: string = point.low?.toFixed(2) ?? '-';
            const close: string = point.close?.toFixed(2) ?? '-';
            hoveredValue = `O: ${open}   H: ${high}   L: ${low}   C: ${close}`;
          } else {
            const valueStr: string = isNaN(point.value) ? '-' : point.value.toFixed(2);
            hoveredValue = `Close: ${valueStr}`;
          }

          canvasRenderingCtx.fillText(hoveredValue, startX, currentY);
          currentY += fontSize + 4; // move down for next line
        }
      }
    });


    // Clear crosshair when mouse leaves chart
    chartDiv.addEventListener('mouseleave', () => {
      canvasRenderingCtx.clearRect(0, 0, canvasWidth, canvasHeight);
    });
  }

  private enableXAxisDrag(): void {
    if (this.canvas == null)
      return;

    let clientX: number = 0;
    this.canvas.addEventListener('mousedown', (event: MouseEvent) => {
      this.isDragging = true;
      clientX = event.clientX;
    });

    this.canvas.addEventListener('mouseup', () => { this.isDragging = false; });
    this.canvas.addEventListener('mouseleave', () => { this.isDragging = false; });

    this.canvas.addEventListener('mousemove', (e) => {
      if (!this.isDragging)
        return;

      const deltaX: number = e.clientX - clientX;
      clientX = e.clientX;

      if (this.viewportStartDate == null || this.viewportEndDate == null || this.overallMinDate == maxDate || this.overallMaxDate == minDate)
        return;

      const viewportTimeRange: number = this.viewportEndDate.getTime() - this.viewportStartDate.getTime();
      if (viewportTimeRange <= 0)
        return;

      const pixelsPerMs: number = this.canvas!.width / viewportTimeRange;
      const deltaTime: number = -deltaX / pixelsPerMs;
      let newStartTime: number = this.viewportStartDate.getTime() + deltaTime;
      let newEndTime: number = this.viewportEndDate.getTime() + deltaTime;

      const minTime: number = this.overallMinDate.getTime();
      const maxTime: number = this.overallMaxDate.getTime();

      // If the user drags too far left, lock the view to the first data point
      if (newStartTime < minTime) {
        newStartTime = minTime;
        newEndTime = newStartTime + viewportTimeRange;
      }

      // If the user drags too far right, lock the view to the last data point
      if (newEndTime > maxTime) {
        newEndTime = maxTime;
        newStartTime = newEndTime - viewportTimeRange;
      }
      this.setViewport(new Date(newStartTime), new Date(newEndTime));
    });
  }

  private enableXAxisZoom(): void {
    if (this.canvas == null)
      return;
    const canvas: HTMLCanvasElement = this.canvas;
    canvas.addEventListener('wheel', (event: WheelEvent) => {
      event.preventDefault();

      if (this.viewportStartDate == null || this.viewportEndDate == null || this.overallMinDate == maxDate || this.overallMaxDate == minDate)
        return;

      const viewportTimeRange: number = this.viewportEndDate.getTime() - this.viewportStartDate.getTime();
      if (viewportTimeRange <= 0)
        return;

      const rect: DOMRect = canvas.getBoundingClientRect();
      const mouseX: number = event.clientX - rect.left;
      const mousePositionRatio: number = mouseX / canvas.width;
      const mousePosTime: number = this.viewportStartDate.getTime() + mousePositionRatio * viewportTimeRange;
      // Determine zoom direction and factor
      const zoomFactor: number = 1.1; // 10% zoom per wheel step
      const zoomIn: boolean = event.deltaY < 0;

      let newViewportTimeRange: number;
      if (zoomIn)
        newViewportTimeRange = viewportTimeRange / zoomFactor; // shrink visible window
      else
        newViewportTimeRange = viewportTimeRange * zoomFactor; // expand visible window

      const maxSpan: number = this.overallMaxDate.getTime() - this.overallMinDate.getTime();
      newViewportTimeRange = Math.min(maxSpan, newViewportTimeRange);

      let newStartTime: number = mousePosTime - mousePositionRatio * newViewportTimeRange;
      let newEndTime: number = mousePosTime + (1 - mousePositionRatio) * newViewportTimeRange;

      const minTime: number = this.overallMinDate.getTime();
      const maxTime: number = this.overallMaxDate.getTime();
      // If zoom goes too far left, lock the view to the first data point
      if (newStartTime < minTime) {
        newStartTime = minTime;
        newEndTime = Math.min(maxTime, newStartTime + newViewportTimeRange);
      }
      // If zoom goes too far right, lock the view to the last data point
      if (newEndTime > maxTime) {
        newEndTime = maxTime;
        newStartTime = Math.max(minTime, newEndTime - newViewportTimeRange);
      }

      this.setViewport(new Date(newStartTime), new Date(newEndTime));
    });
  }

  private drawCandleStick(chartLine: ChartLine, ctx: CanvasRenderingContext2D, xScale: number, yScale: number, canvasHeight: number): void {
    const visibleData: UiChartPoint[] = chartLine.dataSetVisible;
    if (visibleData.length == 0)
      return;

    const visBaseRefValue: number = chartLine.visBaseRefValue;
    if (isNaN(visBaseRefValue) || visBaseRefValue == 0)
      return;
    const visibleCount: number = visibleData.length;
    const totalDataPoints: number = Math.max(visibleCount, 1);
    // Calculate bar width for each data point based on the total canvas width.
    const slotWidth: number = this.xAxis.canvasWidth / totalDataPoints;
    const barWidth: number = Math.max(2, slotWidth * 0.6);

    for (let i = 0; i < visibleData.length; i++) {
      const curr: UiChartPoint = visibleData[i];
      const xCurr: number = (i + 0.5) * slotWidth;
      const open: number = (curr.open / visBaseRefValue) * 100;
      const high: number = (curr.high / visBaseRefValue) * 100;
      const low: number = (curr.low / visBaseRefValue) * 100;
      const close: number = (curr.close / visBaseRefValue) * 100;
      // Convert prices to Y-coordinates
      const yOpen: number = canvasHeight - ((open - this.yAxis.minValue) * yScale);
      const yHigh: number = canvasHeight - ((high - this.yAxis.minValue) * yScale);
      const yLow: number = canvasHeight - ((low - this.yAxis.minValue) * yScale);
      const yClose: number = canvasHeight - ((close - this.yAxis.minValue) * yScale);

      const isBullish: boolean = curr.close >= curr.open;
      const color: string = isBullish ? 'green' : 'red';

      // Wick (High–Low line)
      ctx.strokeStyle = color;
      ctx.beginPath();
      ctx.moveTo(xCurr, yHigh);
      ctx.lineTo(xCurr, yLow);
      ctx.stroke();

      // Candle body (Open–Close)
      const candleTop: number = Math.min(yOpen, yClose);
      const candleBodyHeight: number = Math.abs(yOpen - yClose);
      ctx.fillStyle = color;
      ctx.fillRect(xCurr - barWidth / 2, candleTop, barWidth, candleBodyHeight);
    }
  }

  private drawBasicCandle(chartLine: ChartLine, ctx: CanvasRenderingContext2D, xScale: number, yScale: number, canvasHeight: number): void {
    const visibleData: UiChartPoint[] = chartLine.dataSetVisible;
    if (visibleData.length == 0)
      return;

    const visBaseRefValue: number = chartLine.visBaseRefValue;
    if (isNaN(visBaseRefValue) || visBaseRefValue == 0)
      return;
    const totalDataPoints = Math.max(visibleData.length, 1);
    // Calculate bar width for each data point based on the total canvas width.
    const slotWidth = this.xAxis.canvasWidth / totalDataPoints;
    const barWidth = slotWidth * 0.6;
    for (let i = 1; i < visibleData.length; i++) {
      const prev: UiChartPoint = visibleData[i - 1];
      const curr: UiChartPoint = visibleData[i];

      const xCurr: number = (i + 0.5) * slotWidth;
      const yPrev: number = canvasHeight - (((prev.value / visBaseRefValue) * 100 - this.yAxis.minValue) * yScale);
      const yCurr: number = canvasHeight - (((curr.value / visBaseRefValue) * 100 - this.yAxis.minValue) * yScale);

      const top: number = Math.min(yPrev, yCurr);
      const height: number = Math.abs(yPrev - yCurr);

      ctx.fillStyle = curr.value >= prev.value ? 'green' : 'red';
      ctx.fillRect(xCurr - barWidth / 2, top, barWidth, height);
    }
  }

  private drawLine(chartLine: ChartLine, ctx: CanvasRenderingContext2D, xScale: number, yScale: number, canvasHeight: number): void {
    const visibleData: UiChartPoint[] = chartLine.dataSetVisible;
    if (visibleData.length == 0)
      return;

    ctx.beginPath();
    const totalDataPoints: number = Math.max(visibleData.length, 1);
    const slotWidth: number = this.xAxis.canvasWidth / totalDataPoints;
    for (let i = 0; i < visibleData.length; i++) {
      const point: UiChartPoint = visibleData[i];
      const x: number = (i + 0.5) * slotWidth;
      const percentValue: number = (point.value / chartLine.visBaseRefValue) * 100;
      const y: number = canvasHeight - ((percentValue - this.yAxis.minValue) * yScale);
      if (i == 0)
        ctx.moveTo(x, y);
      else
        ctx.lineTo(x, y);
    }
    ctx.strokeStyle = chartLine.color ?? 'black';
    ctx.stroke();
  }
}