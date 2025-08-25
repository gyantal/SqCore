
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

  public getVisibleRange(): [number, number] {
    return [this.visibleDataStartIdx, this.visibleDataEndIdx];
  }
}

class XAxis {
  private minTime: number = 0;
  private maxTime: number = 1;
  private canvasWidth: number = 1;

  public setDomain(data: UiChartPoint[], canvasWidth: number) {
    this.minTime = data[0].date.getTime();
    this.maxTime = data[data.length - 1].date.getTime();
    this.canvasWidth = canvasWidth;
  }

  public generateTicks(data: UiChartPoint[]): { time: number, label: string }[] {
    if (data.length < 2)
      return [];

    const startDate: Date = data[0].date;
    const endDate: Date = data[data.length - 1].date;
    const current: Date = new Date(startDate);
    current.setDate(1); // Align to first of the month
    const ticks: { time: number, label: string }[] = [];
    while (current <= endDate) {
      const month: number = current.getMonth();
      const label: string = month === 0 ? current.getFullYear().toString() : current.toLocaleDateString('en-US', { month: 'short' }); // Feb, Mar, etc.

      ticks.push({ time: current.getTime(), label });
      current.setMonth(current.getMonth() + 1);
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

    this.setDomain(data, canvasWidth);

    // Draw ticks and labels
    const ticks: { time: number; label: string; }[] = this.generateTicks(data);
    const timeRange: number = this.maxTime - this.minTime || 1; // Avoid division by zero
    const xScale: number = canvasWidth / timeRange;

    for (let i = 0; i < ticks.length; i++) {
      const { time, label } = ticks[i];
      const x: number = (time - this.minTime) * xScale;

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

  public scaleXToTime(xPixel: number): Date { // Converts a horizontal pixel coordinate on the canvas to a Date.
    const time: number = this.minTime + (xPixel / this.canvasWidth) * (this.maxTime - this.minTime);
    return new Date(time);
  }
}

class YAxis {
  private minValue: number = 0;
  private maxValue: number = 1;
  private canvasHeight: number = 1;

  public setDomain(data: UiChartPoint[], canvasHeight: number): void {
    const values: number[] = data.map((d) => d.value);
    this.minValue = Math.min(...values);
    this.maxValue = Math.max(...values);
    this.canvasHeight = canvasHeight;
  }

  public generateTicks(data: UiChartPoint[]): number[] {
    if (data.length == 0)
      return [];

    // Extract all values and compute min/max
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
    this.setDomain(data, this.canvasHeight);
    // Draw ticks, labels
    const ticks = this.generateTicks(data);

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
  private widthPercent = 0.97;
  private heightPercent = 0.9;
  private xAxis: XAxis = new XAxis();
  private yAxis: YAxis = new YAxis();

  // Drag state
  private isDragging = false;

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

    this.enableXAxisDrag();
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

    this.displayTooltip(this.chartDiv!, canvasWidth, canvasHeight, this.xAxis, this.yAxis);

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
    xAxisCanvas.height = this.canvas.height * (1 - this.heightPercent);
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

    chartDiv.addEventListener('mousemove', function(event) {
      canvasRenderingCtx.clearRect(0, 0, canvasWidth, canvasHeight); // Clear canvas

      const chartDivRect: DOMRect = chartDiv.getBoundingClientRect();
      const x: number = event.clientX - chartDivRect.left;
      const y: number = event.clientY - chartDivRect.top;

      // Compute values
      const date: Date = xAxis.scaleXToTime(x);
      const dateStr: string = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
      const priceStr: string = yAxis.scaleYToValue(y).toFixed(2);
      // Draw vertical line
      canvasRenderingCtx.beginPath();
      canvasRenderingCtx.moveTo(x, 0);
      canvasRenderingCtx.lineTo(x, canvasHeight);
      canvasRenderingCtx.strokeStyle = 'green';
      canvasRenderingCtx.setLineDash([5, 5]);
      canvasRenderingCtx.stroke();

      // Draw horizontal line
      canvasRenderingCtx.beginPath();
      canvasRenderingCtx.moveTo(0, y);
      canvasRenderingCtx.lineTo(canvasWidth, y);
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
      const priceBoxY: number = y - priceTextHeight / 2 - paddingY;

      canvasRenderingCtx.fillStyle = 'black';
      canvasRenderingCtx.fillRect(priceBoxX, priceBoxY, priceTextWidth + paddingX * 2, priceTextHeight + paddingY * 2);

      canvasRenderingCtx.fillStyle = 'white';
      canvasRenderingCtx.fillText(priceStr, priceBoxX + paddingX, priceBoxY + priceTextHeight + paddingY / 2);

      // Draw date on bottom (X axis)
      const dateTextWidth: number = canvasRenderingCtx.measureText(dateStr).width;
      const dateTextHeight: number = fontSize;

      const dateBoxX: number = x - dateTextWidth / 2 - paddingX;
      const dateBoxY: number = canvasHeight - dateTextHeight - paddingY * 2;

      canvasRenderingCtx.fillStyle = 'black';
      canvasRenderingCtx.fillRect(dateBoxX, dateBoxY, dateTextWidth + paddingX * 2, dateTextHeight + paddingY * 2);

      canvasRenderingCtx.fillStyle = 'white';
      canvasRenderingCtx.fillText(dateStr, dateBoxX + paddingX, dateBoxY + dateTextHeight + paddingY / 2);
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

      if (this.chartLines.length == 0)
        return;

      const firstLine: ChartLine = this.chartLines[0];
      const visibleData: UiChartPoint[] = firstLine.getVisibleData();
      if (visibleData.length == 0)
        return;

      const totalVisible: number = visibleData.length;
      const pxPerPoint: number = this.canvas!.width / totalVisible; // Compute how many pixels correspond to one data point across the canvas
      const shiftPoints: number = Math.round(-deltaX / pxPerPoint); // Convert pixel drag distance to number of data points to shift

      for (const line of this.chartLines) {
        const data: UiChartPoint[] = line.getData();
        const [startIdx, endIdx] = line.getVisibleRange();
        let newStart: number = startIdx + shiftPoints;
        let newEnd: number = endIdx + shiftPoints;

        // If the user drags too far left, lock the view to the first chunk of data(e.g, if 50 points fit on screen -> show points 0-49)
        if (newStart < 0) {
          newStart = 0;
          newEnd = totalVisible - 1;
        }

        // If the user drags too far right, lock the view to the last chunk of data(e.g, if 50 points fit on screen and the dataset has 200 points-> show points 150-199)
        if (newEnd >= data.length) {
          newEnd = data.length - 1;
          newStart = newEnd - totalVisible + 1;
        }

        line.setVisibleRange(newStart, newEnd);
      }

      this.redraw();
    });
  }
}