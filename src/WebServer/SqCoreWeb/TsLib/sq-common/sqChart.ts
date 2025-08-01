
// Usage example.
// // Sample data
// const chartData: ChartDataPoint[] = [
//   { date: new Date('2025-01-01'), value: 100 },
//   { date: new Date('2025-02-01'), value: 150 },
//   { date: new Date('2025-03-01'), value: 200 },
// ];

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


// Chart line class to manage individual data series
class ChartLine {
  private data: UiChartPoint[];
  private visibleDataStartIdx: number;
  private visibleDataEndIdx: number;

  constructor(data: UiChartPoint[] = []) {
    this.data = data;
    this.visibleDataStartIdx = 0;
    this.visibleDataEndIdx = data.length - 1;
  }

  // Getters
  public getData(): UiChartPoint[] {
    return this.data;
  }

  public getVisibleData(): UiChartPoint[] {
    return this.data.slice(this.visibleDataStartIdx, this.visibleDataEndIdx + 1);
  }

  // Setters
  public setData(data: UiChartPoint[]): void {
    this.data = data;
    this.resetVisibleIndices();
  }

  public setVisibleRange(startIdx: number, endIdx: number): void {
    if (startIdx >= 0 && endIdx < this.data.length && startIdx <= endIdx) {
      this.visibleDataStartIdx = startIdx;
      this.visibleDataEndIdx = endIdx;
    }
  }

  private resetVisibleIndices(): void {
    this.visibleDataStartIdx = 0;
    this.visibleDataEndIdx = this.data.length - 1;
  }
}

class XAxis {
  public generateTicks(data: UiChartPoint[]): { time: number, label: string }[] {
    if (data.length < 2)
      return [];

    const startDate: Date = data[0].date;
    const endDate: Date = data[data.length - 1].date;
    const range: number = endDate.getTime() - startDate.getTime();
    const msPerDay: number = 24 * 60 * 60 * 1000; // Number of milliseconds in a day
    const msPerMonth: number = 30 * msPerDay;
    const msPerYear: number = 365 * msPerDay;
    // Determine appropriate step size and format based on range
    let stepSize: number;
    let formatLabel: (date: Date) => string;
    if (range <= 7 * msPerDay) { // Less than a week: use days
      stepSize = msPerDay;
      formatLabel = (date) => date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    } else if (range <= 6 * msPerMonth) { // Less than 6 months: use months
      stepSize = msPerMonth;
      formatLabel = (date) => date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
    } else { // Years
      stepSize = msPerYear;
      formatLabel = (date) => date.getFullYear().toString();
    }

    const ticks: { time: number, label: string }[] = [];
    let current: number = Math.floor(startDate.getTime() / stepSize) * stepSize;
    while (current <= endDate.getTime()) {
      ticks.push({ time: current, label: formatLabel(new Date(current)) });
      current += stepSize;
    }

    return ticks;
  }

  public render(ctx: CanvasRenderingContext2D, canvasWidth: number, data: UiChartPoint[]): void {
    if (data.length < 2)
      return;

    ctx.beginPath();
    // Draw axis line (top of canvas)
    ctx.moveTo(0, 0);
    ctx.lineTo(canvasWidth, 0);
    ctx.stroke();

    // Draw ticks and labels
    const ticks: { time: number; label: string; }[] = this.generateTicks(data);
    const minTime: number = data[0].date.getTime();
    const maxTime: number = data[data.length - 1].date.getTime();
    const timeRange: number = maxTime - minTime || 1; // Avoid division by zero
    const xScale: number = canvasWidth / timeRange;

    for (let i = 0; i < ticks.length; i++) {
      const { time, label } = ticks[i];
      const x: number = (time - minTime) * xScale;

      // Draw tick mark
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, 5);
      ctx.stroke();

      // Draw tick label
      const textWidth: number = ctx.measureText(label).width;
      ctx.fillStyle = 'black';
      ctx.fillText(label, x - textWidth / 2, 15);
    }
  }
}

class YAxis {
  public generateTicks(data: UiChartPoint[]): number[] {
    if (data.length === 0) return [];

    // Extract all values and compute min/max
    const values: number[] = data.map((d) => d.value);
    const minValue: number = Math.min(...values);
    const maxValue: number = Math.max(...values);
    const valueRange: number = maxValue - minValue;
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
    const roundedMin: number = Math.floor(minValue / niceStep) * niceStep;
    const roundedMax: number = Math.ceil(maxValue / niceStep) * niceStep;

    // Generate ticks from roundedMin to roundedMax
    const ticks: number[] = [];
    for (let tick = roundedMin; tick <= roundedMax; tick += niceStep)
      ticks.push(tick);

    return ticks;
  }

  public render(ctx: CanvasRenderingContext2D, canvasHeight: number, data: UiChartPoint[]): void {
    if (data.length == 0)
      return;

    const tickLabelXOffset = 10; // Controls the horizontal position of the tick labels relative to the axis.
    const tickLabelYOffset = 4; // Adjusts the vertical alignment of the tick labels to ensure they are visually centered relative to the tick marks.
    const tickMarkLength = 5; // Length of tick marks on the axis
    // Draw axis line (left side of canvas)
    ctx.moveTo(0, 0);
    ctx.lineTo(0, canvasHeight);
    ctx.stroke();

    // Draw ticks, labels
    const ticks = this.generateTicks(data);
    const values = data.map((d) => d.value);
    let min = Math.min(...values);
    let max = Math.max(...values);

    // Add 10% padding to min and max to prevent ticks at canvas edges
    const range = max - min;
    const padding = range * 0.01;
    min -= padding;
    max += padding;
    const yScale = max > min ? canvasHeight / (max - min) : 1;

    for (let i = 0; i < ticks.length; i++) {
      const tick: number = ticks[i];
      const y: number = canvasHeight - (tick - min) * yScale;

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
}

// Main chart class
export class SqChart {
  private chartLines: ChartLine[] = [];
  private chartDiv: HTMLElement | null = null;
  private canvas: HTMLCanvasElement | null = null;
  private widthPercent = 0.97;
  private heightPercent = 0.9;
  private xAxis: XAxis = new XAxis();
  private yAxis: YAxis = new YAxis();

  public init(chartDiv: HTMLElement): void {
    this.chartDiv = chartDiv;
    const chartDivWidth = this.chartDiv.clientWidth as number;
    const chartDivHeight = this.chartDiv.clientHeight as number;
    const canvas = document.createElement('canvas');
    // Allocate space for X and Y axes by adjusting the canvas size
    canvas.width = chartDivWidth * this.widthPercent; // 97% of the ChartDiv Width
    canvas.height = chartDivHeight * this.heightPercent; // 90% of the ChartDiv Height

    this.chartDiv.appendChild(canvas);
    this.canvas = canvas;
    this.redraw();
    // ResizeObserver - Ensures the canvas stays correctly sized when the chart container is resized by user or layout change. see https://developer.mozilla.org/en-US/docs/Web/API/ResizeObserver
    new ResizeObserver(() => { this.resizeCanvasToContainer(); }).observe(this.chartDiv);
  }

  public addLine(data: UiChartPoint[]): ChartLine {
    const line = new ChartLine(data);
    this.chartLines.push(line);
    this.redraw();
    return line;
  }

  public setViewport(startDate: Date, endDate: Date): void {
    for (const line of this.chartLines) {
      const chrtData: UiChartPoint[] = line.getData();
      let startIdx: number = 0;
      let endIdx: number = chrtData.length - 1;

      for (let i = 0; i < chrtData.length; i++) {
        if (chrtData[i].date >= startDate) {
          startIdx = i;
          break;
        }
      }

      for (let i = chrtData.length - 1; i >= 0; i--) {
        if (chrtData[i].date <= endDate) {
          endIdx = i;
          break;
        }
      }

      line.setVisibleRange(startIdx, endIdx);
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
    canvasRenderingCtx.fillStyle = '#6bf366ff'; // adding the  background
    canvasRenderingCtx.fillRect(0, 0, canvasWidth, canvasHeight);

    canvasRenderingCtx.beginPath();
    let visibleData: UiChartPoint[] | null = null;
    for (const line of this.chartLines) {
      visibleData = line.getVisibleData();
      if (visibleData.length == 0)
        continue;

      // Basic line drawing logic (simplified)
      const xScale: number = canvasWidth / (visibleData.length - 1);
      const yScale: number = canvasHeight / Math.max(...visibleData.map((d) => d.value));

      canvasRenderingCtx.moveTo(0, canvasHeight - visibleData[0].value * yScale);
      for (let i = 1; i < visibleData.length; i++) {
        const x: number = i * xScale;
        const y: number = canvasHeight - visibleData[i].value * yScale;
        canvasRenderingCtx.lineTo(x, y);
      }
    }
    canvasRenderingCtx.strokeStyle = '#007bff'; // line color
    canvasRenderingCtx.stroke();

    this.drawAxes(visibleData);

    // displaying the chart dimesions for debugging
    if (visibleData != null) {
      const x0: string = visibleData[0].date.toDateString();
      const y0: number = visibleData[0].value;
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
    this.canvas.width = chartDivRect.width * this.widthPercent;
    this.canvas.height = chartDivRect.height * this.heightPercent;

    this.redraw(); // Redraw chart with new canvas size
  }

  private drawAxes(visibleData: UiChartPoint[] | null): void {
    if (visibleData == null || this.chartDiv == null || this.canvas == null)
      return;

    // Remove old xAxisCanvas
    const existingXAxis: Element | null = this.chartDiv.querySelector('#xAxisCanvas');
    if (existingXAxis != null)
      this.chartDiv.removeChild(existingXAxis);

    // Create the xAxis canvas
    const xAxisCanvas: HTMLCanvasElement = document.createElement('canvas');
    xAxisCanvas.id = 'xAxisCanvas';
    xAxisCanvas.width = this.canvas.width;
    xAxisCanvas.style.position = 'absolute';
    xAxisCanvas.style.top = `${this.canvas.height}px`;
    xAxisCanvas.style.left = '0px'; // align with chart

    // Append the xAxis canvas
    this.chartDiv.appendChild(xAxisCanvas);
    const xAxisCtx: CanvasRenderingContext2D | null = xAxisCanvas.getContext('2d');
    if (xAxisCtx == null)
      return;

    this.xAxis.render(xAxisCtx, this.canvas.width, visibleData);

    // Remove old yAxisCanvas
    const existingYAxis: Element | null = this.chartDiv.querySelector('#yAxisCanvas');
    if (existingYAxis != null)
      this.chartDiv.removeChild(existingYAxis);
    // Create the yAxis canvas
    const yAxisCanvas: HTMLCanvasElement = document.createElement('canvas');
    yAxisCanvas.id = 'yAxisCanvas';
    yAxisCanvas.width = this.canvas.width * (1 - this.widthPercent);
    yAxisCanvas.height = this.canvas.height;
    yAxisCanvas.style.position = 'absolute';
    yAxisCanvas.style.left = `${this.canvas.width}px`; // chart starts at margin.left
    // Append the yAxis canvas
    this.chartDiv.appendChild(yAxisCanvas);
    const yAxisCtx: CanvasRenderingContext2D | null = yAxisCanvas.getContext('2d');
    if (yAxisCtx == null)
      return;

    this.yAxis.render(yAxisCtx, this.canvas.height, visibleData);
  }
}