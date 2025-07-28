
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

// Main chart class
export class SqChart {
  private chartLines: ChartLine[] = [];
  private chartDiv: HTMLElement | null = null;
  private canvas: HTMLCanvasElement | null = null;
  private widthPercent = 0.97;
  private heightPercent = 0.9;

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

    const canvasWwidth: number = this.canvas.width;
    const canvasHeight: number = this.canvas.height;

    // Clear
    canvasRenderingCtx.clearRect(0, 0, canvasWwidth, canvasHeight);
    canvasRenderingCtx.fillStyle = '#6bf366ff'; // adding the  background
    canvasRenderingCtx.fillRect(0, 0, canvasWwidth, canvasHeight);

    canvasRenderingCtx.beginPath();
    let visibleData: UiChartPoint[] | null = null;
    for (const line of this.chartLines) {
      visibleData = line.getVisibleData();
      if (visibleData.length == 0)
        continue;

      // Basic line drawing logic (simplified)
      const xScale: number = canvasWwidth / (visibleData.length - 1);
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
      const text: string = `x0: ${x0}, y0: ${y0}, width: ${canvasWwidth}, height: ${canvasHeight}`;

      canvasRenderingCtx.font = '14px sans-serif';
      canvasRenderingCtx.fillStyle = '#000000';
      const textWidth: number = canvasRenderingCtx.measureText(text).width;
      canvasRenderingCtx.fillText( text, (canvasWwidth - textWidth) / 2, canvasHeight / 2 );
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
    const xAxisCtx = xAxisCanvas.getContext('2d');
    if (xAxisCtx == null)
      return;
    xAxisCtx.fillStyle = '#f36666ff'; // adding the  background for debugging
    xAxisCtx.fillRect(0, 0, this.canvas.width, this.canvas.height * (1 - this.heightPercent));

    // Remove old yAxisCanvas
    const existingYAxis: Element | null = this.chartDiv.querySelector('#yAxisCanvas');
    if (existingYAxis != null)
      this.chartDiv.removeChild(existingYAxis);
    // Create the yAxis canvas
    const yAxisCanvas = document.createElement('canvas');
    yAxisCanvas.id = 'yAxisCanvas';
    yAxisCanvas.width = this.canvas.width * (1 - this.widthPercent);
    yAxisCanvas.height = this.canvas.height;
    yAxisCanvas.style.position = 'absolute';
    yAxisCanvas.style.left = `${this.canvas.width}px`; // chart starts at margin.left
    // Append the yAxis canvas
    this.chartDiv.appendChild(yAxisCanvas);
    const yAxisCtx = yAxisCanvas.getContext('2d');
    if (yAxisCtx == null)
      return;
    yAxisCtx.fillStyle = '#FFFF8F'; // adding the  background for debugging
    yAxisCtx.fillRect(0, 0, this.canvas.width * (1 - this.heightPercent), this.canvas.height);
  }
}