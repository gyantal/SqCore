
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
  private chartLines: ChartLine[];
  private chartDiv: HTMLElement | null;
  private width: number;
  private height: number;
  private margin: {top: number; right: number; bottom: number; left: number;} = {top: 30, right: 30, bottom: 30, left: 30};

  constructor() {
    this.chartLines = [];
    this.chartDiv = null;
    this.width = 0;
    this.height = 0;
  }

  public init(chartDiv: HTMLElement, width: number, height: number): void {
    this.chartDiv = chartDiv;
    this.width = width;
    this.height = height;
    this.redraw();
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

  private redraw(): void {
    if (this.chartDiv == null)
      return;

    // Clear the chart div
    this.chartDiv.innerHTML = '';

    // Create a canvas and render
    const canvas: HTMLCanvasElement = document.createElement('canvas');
    canvas.width = this.width - this.margin.left - this.margin.right;
    canvas.height = this.height- this.margin.top - this.margin.bottom;
    this.chartDiv.appendChild(canvas);

    const canvasRenderingCtx: CanvasRenderingContext2D | null = canvas.getContext('2d');
    if (canvasRenderingCtx == null)
      return;

    console.log(canvasRenderingCtx);

    canvasRenderingCtx.fillStyle = '#6bf366ff'; // adding the  background
    canvasRenderingCtx.fillRect(this.margin.left, this.margin.top, canvas.width, canvas.height);
    canvasRenderingCtx.beginPath();
    let visibleData: UiChartPoint[] | null = null;
    for (const line of this.chartLines) {
      visibleData = line.getVisibleData();
      if (visibleData.length == 0)
        continue;

      // Basic line drawing logic (simplified)
      const xScale: number = canvas.width / (visibleData.length - 1);
      const yScale: number = canvas.height / Math.max(...visibleData.map((d) => d.value));

      canvasRenderingCtx.moveTo(this.margin.left, canvas.height - visibleData[0].value * yScale);
      for (let i = 1; i < visibleData.length; i++) {
        const x: number = this.margin.left + i * xScale;
        const y: number = this.height - this.margin.bottom - visibleData[i].value * yScale;
        canvasRenderingCtx.lineTo(x, y);
      }
    }
    canvasRenderingCtx.strokeStyle = '#007bff'; // line color
    canvasRenderingCtx.stroke();

    // displaying the chart dimesions for debugging
    if (visibleData != null) {
      const x0: string = visibleData[0].date.toDateString();
      const y0: number = visibleData[0].value;
      const text: string = `x0: ${x0}, y0: ${y0}, width: ${canvas.width}, height: ${canvas.height}`;

      canvasRenderingCtx.font = '14px sans-serif';
      canvasRenderingCtx.fillStyle = '#000000';
      const textWidth: number = canvasRenderingCtx.measureText(text).width;

      canvasRenderingCtx.fillText( text, (canvas.width - textWidth) / 2, canvas.height / 2 );
    }
  }
}