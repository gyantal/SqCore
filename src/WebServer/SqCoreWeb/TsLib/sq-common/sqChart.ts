
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
  private canvas: HTMLCanvasElement | null;
  private width: number;
  private height: number;
  private margin: {top: number; right: number; bottom: number; left: number;} = {top: 30, right: 30, bottom: 30, left: 30};
  private minChartWidthPercent: number = 10;
  private minChartHeightVh: number = 10;

  constructor() {
    this.chartLines = [];
    this.chartDiv = null;
    this.canvas = null;
    this.width = 0;
    this.height = 0;
  }

  public init(chartDiv: HTMLElement): void {
    this.chartDiv = chartDiv;
    this.redraw();
    this.resizeCanvasToContainer();
    this.resizer('horizontal'); // resizing the width
    this.resizer('vertical'); // resizing the height
    window.addEventListener('resize', () => this.resizeCanvasToContainer());
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
    this.width = this.chartDiv.clientWidth as number;
    this.height = this.chartDiv.clientHeight as number;
    console.log(`this.width: ${this.width} and this.height: ${this.height}`);
    // Remove existing canvas only, keep resizers
    const existingCanvas: HTMLCanvasElement | null = this.chartDiv.querySelector('canvas');
    if (existingCanvas != null)
      this.chartDiv.removeChild(existingCanvas);

    // Create a canvas and render
    this.canvas = this.setupCanvas();
    this.chartDiv.appendChild(this.canvas);

    const canvasRenderingCtx: CanvasRenderingContext2D | null = this.canvas.getContext('2d');
    if (canvasRenderingCtx == null)
      return;

    console.log(canvasRenderingCtx);

    canvasRenderingCtx.fillStyle = '#6bf366ff'; // adding the  background
    canvasRenderingCtx.fillRect(this.margin.left, this.margin.top, this.canvas.width, this.canvas.height);
    canvasRenderingCtx.beginPath();
    let visibleData: UiChartPoint[] | null = null;
    for (const line of this.chartLines) {
      visibleData = line.getVisibleData();
      if (visibleData.length == 0)
        continue;

      // Basic line drawing logic (simplified)
      const xScale: number = this.canvas.width / (visibleData.length - 1);
      const yScale: number = this.canvas.height / Math.max(...visibleData.map((d) => d.value));

      canvasRenderingCtx.moveTo(this.margin.left, this.canvas.height - visibleData[0].value * yScale);
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
      const text: string = `x0: ${x0}, y0: ${y0}, width: ${this.canvas.width}, height: ${this.canvas.height}`;

      canvasRenderingCtx.font = '14px sans-serif';
      canvasRenderingCtx.fillStyle = '#000000';
      const textWidth: number = canvasRenderingCtx.measureText(text).width;
      canvasRenderingCtx.fillText( text, (this.canvas.width - textWidth) / 2, this.canvas.height / 2 );
    }
  }

  private setupCanvas(): HTMLCanvasElement {
    const canvas = document.createElement('canvas');
    canvas.id = 'canvas';
    canvas.width = this.width - this.margin.left - this.margin.right;
    canvas.height = this.height - this.margin.top - this.margin.bottom;
    return canvas;
  }

  private resizer(resizeDirection: string) {
    const resizerBar: HTMLElement | null = resizeDirection == 'horizontal' ? document.getElementById('widthResizer') : document.getElementById('heightResizer');
    if (resizerBar == null || this.chartDiv == null || this.canvas == null)
      return;

    resizerBar!.addEventListener('mousedown', (event) => {
      event.preventDefault();
      const chartRect: DOMRect = this.chartDiv!.getBoundingClientRect();
      const chartDivParentEle: HTMLElement = this.chartDiv!.parentElement!;
      const originalMouseX: number = event.pageX;
      const originalMouseY: number = event.pageY;
      const chartDivParentWidth: number = chartDivParentEle.getBoundingClientRect().width;

      const onMouseMove = (moveEvent: MouseEvent) => {
        if (resizeDirection == 'horizontal') {
          const newWidthPx = chartRect.width - (originalMouseX - moveEvent.pageX);
          let calculatedWidthPercent = (newWidthPx / chartDivParentWidth) * 100;
          calculatedWidthPercent = Math.max(this.minChartWidthPercent, Math.min(99.5, calculatedWidthPercent));
          this.chartDiv!.style.width = `${calculatedWidthPercent}%`;
          this.canvas!.style.width = `${calculatedWidthPercent}%`;
        } else if (resizeDirection == 'vertical') {
          const originalHeight: number = chartRect.height;
          const deltaY: number = moveEvent.pageY - originalMouseY;
          const newHeightPx: number = originalHeight + deltaY;
          let calculatedHeightVh = (newHeightPx / window.innerHeight) * 100;
          calculatedHeightVh = Math.max(this.minChartHeightVh, Math.min(95, calculatedHeightVh));
          this.chartDiv!.style.height = `${calculatedHeightVh}vh`;
          this.canvas!.style.height = `${calculatedHeightVh}vh`;
        }
        this.resizeCanvasToContainer();
      };

      const onMouseUp = () => {
        window.removeEventListener('mousemove', onMouseMove);
        window.removeEventListener('mouseup', onMouseUp);
      };

      window.addEventListener('mousemove', onMouseMove);
      window.addEventListener('mouseup', onMouseUp);
    });
  }

  // we need to match the canvas dimensions to the container(ChartDiv), when the user resizes either by manually or window.
  private resizeCanvasToContainer(): void {
    const chartDivRect: DOMRect = this.chartDiv!.getBoundingClientRect();
    this.canvas!.width = chartDivRect.width;
    this.canvas!.height = chartDivRect.height;

    this.redraw(); // Redraw chart with new canvas size
  }
}