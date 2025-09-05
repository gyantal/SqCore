
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


// Chart line class to manage individual data series
class ChartLine {
  private dataSets: UiChartPoint[][];
  public visibleDataStartIdx: number;
  public visibleDataEndIdx: number;

  constructor(dataSets: UiChartPoint[][] = []) {
    this.dataSets = dataSets;
    this.visibleDataStartIdx = 0;
    this.visibleDataEndIdx = dataSets.length > 0 ? dataSets[0].length - 1 : 0;
  }

  // Getters
  public getDataSets(): UiChartPoint[][] {
    return this.dataSets;
  }

  public getVisibleData(): UiChartPoint[][] {
    return this.dataSets.map((ds) => ds.slice(this.visibleDataStartIdx, this.visibleDataEndIdx + 1));
  }

  // Setters
  public setDataSets(dataSets: UiChartPoint[][]): void {
    this.dataSets = dataSets;
    this.resetVisibleIndices();
  }

  public setVisibleRange(startIdx: number, endIdx: number): void {
    if (startIdx >= 0 && this.dataSets.length > 0 && endIdx < this.dataSets[0].length && startIdx <= endIdx) {
      this.visibleDataStartIdx = startIdx;
      this.visibleDataEndIdx = endIdx;
    }
  }

  private resetVisibleIndices(): void {
    this.visibleDataStartIdx = 0;
    this.visibleDataEndIdx = this.dataSets.length > 0 ? this.dataSets[0].length - 1 : 0;
  }
}

class XAxis {
  public minTime: number = 0;
  public maxTime: number = 1;
  public canvasWidth: number = 1;


  public generateTicks(dataSets: UiChartPoint[][]): { time: number, label: string }[] {
    if (dataSets.length == 0)
      return [];

    let minStarDate: Date = maxDate;
    let maxEndDate: Date = minDate;
    // Find start and end dates from the datasets
    for (let i = 0; i < dataSets.length; i++) {
      const dataset: UiChartPoint[] = dataSets[i];
      if (dataset.length == 0)
        continue;

      const startDate: Date = dataset[0].date;
      const endDate: Date = dataset[dataset.length - 1].date;
      if (startDate < minStarDate)
        minStarDate = startDate;

      if (endDate > maxEndDate)
        maxEndDate = endDate;
    }

    this.minTime = minStarDate.getTime();
    this.maxTime = maxEndDate.getTime();

    const current: Date = new Date(minStarDate);
    current.setDate(1); // Align to first of the month
    const ticks: { time: number, label: string }[] = [];
    while (current <= maxEndDate) {
      const month: number = current.getMonth();
      const label: string = month === 0 ? current.getFullYear().toString() : current.toLocaleDateString('en-US', { month: 'short' }); // Feb, Mar, etc.

      ticks.push({ time: current.getTime(), label });
      current.setMonth(current.getMonth() + 1);
    }

    return ticks;
  }

  public render(ctx: CanvasRenderingContext2D, canvasWidth: number, dataSets: UiChartPoint[][]): void {
    if (dataSets.length == 0)
      return;

    ctx.beginPath();
    // Draw axis line (top of canvas)
    ctx.moveTo(0, 0);
    ctx.lineTo(canvasWidth, 0);
    ctx.stroke();

    this.canvasWidth = canvasWidth;

    // Draw ticks and labels
    const ticks: { time: number; label: string; }[] = this.generateTicks(dataSets);
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
  public minValue: number = Number.MAX_VALUE; // Initialize with a large value;
  public maxValue: number = Number.MIN_VALUE; // Initialize with a small value
  public canvasHeight: number = 1;

  public generateTicks(dataSets: UiChartPoint[][]): number[] {
    if (dataSets.length == 0)
      return [];
    // Reset
    this.minValue = Number.MAX_VALUE;
    this.maxValue = -Number.MAX_VALUE;

    // Find min and max value
    for (const dataset of dataSets) {
      for (const point of dataset) {
        if (point.value < this.minValue)
          this.minValue = point.value;
        if (point.value > this.maxValue)
          this.maxValue = point.value;
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

  public render(ctx: CanvasRenderingContext2D, canvasHeight: number, data: UiChartPoint[][]): void {
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

  private pixelsPerPeriod: number = 1; // horizontal zoom level

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

  public addLine(data: UiChartPoint[][]): void {
    const line = new ChartLine(data);
    this.chartLines.push(line);
    this.redraw();
  }

  public setViewport(startDate: Date, endDate: Date): void {
    for (const line of this.chartLines) {
      const dataSets: UiChartPoint[][] = line.getDataSets();

      for (let i = 0; i < dataSets.length; i++) {
        const dataSet: UiChartPoint[] = dataSets[i];
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

        line.setVisibleRange(startIdx, endIdx);
      }
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

    // Collect all visible data
    const visibleDataSets: UiChartPoint[][] = [];
    for (const line of this.chartLines) {
      const visibleData: UiChartPoint[][] = line.getVisibleData();
      visibleDataSets.push(...visibleData);
    }

    if (visibleDataSets.length == 0)
      return;

    // Basic line drawing logic (simplified)
    for (const visibleData of visibleDataSets) {
      if (visibleData.length < 2)
        continue;
      const xScale = canvasWidth / (this.xAxis.maxTime - this.xAxis.minTime);
      const yScale = canvasHeight / (this.yAxis.maxValue - this.yAxis.minValue);
      canvasRenderingCtx.beginPath();
      let firstVal: boolean = true;
      for (const point of visibleData) {
        const x: number = (point.date.getTime() - this.xAxis.minTime) * xScale;
        const y: number = canvasHeight - ((point.value - this.yAxis.minValue) * yScale);
        if (firstVal) {
          canvasRenderingCtx.moveTo(x, y);
          firstVal = false;
        } else
          canvasRenderingCtx.lineTo(x, y);
      }
      canvasRenderingCtx.strokeStyle = '#007bff'; // line color
      canvasRenderingCtx.stroke();
    }

    this.drawAxes(visibleDataSets);

    this.displayTooltip(this.chartDiv!, canvasWidth, canvasHeight, this.xAxis, this.yAxis);

    // displaying the chart dimesions for debugging
    if (visibleDataSets != null) {
      const x0: string = visibleDataSets[0][0].date.toDateString();
      const y0: number = visibleDataSets[0][0].value;
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

  private drawAxes(visibleData: UiChartPoint[][] | null): void {
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
    xAxisCanvas.height = this.canvas.height * (1 - this.canvasHeightPercent);
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
    yAxisCanvas.width = this.canvas.width * (1 - this.canvasWidthPercent);
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
      const visibleData: UiChartPoint[][] = firstLine.getVisibleData();
      if (visibleData.length == 0)
        return;

      const totalVisible: number = visibleData[0].length;
      const pxPerPoint: number = this.canvas!.width / totalVisible; // Compute how many pixels correspond to one data point across the canvas
      const shiftPoints: number = Math.round(-deltaX / pxPerPoint); // Convert pixel drag distance to number of data points to shift

      // update the visible range
      const [startIdx, endIdx] = [firstLine.visibleDataStartIdx, firstLine.visibleDataEndIdx]; // get the current Visible Range
      let newStart: number = startIdx + shiftPoints;
      let newEnd: number = endIdx + shiftPoints;
      const totalDataLength = firstLine.getDataSets()[0].length;

      // If the user drags too far left, lock the view to the first chunk of data(e.g, if 50 points fit on screen -> show points 0-49)
      if (newStart < 0) {
        newStart = 0;
        newEnd = totalVisible - 1;
      }

      // If the user drags too far right, lock the view to the last chunk of data(e.g, if 50 points fit on screen and the dataset has 200 points-> show points 150-199)
      if (newEnd >= totalDataLength) {
        newEnd = totalDataLength - 1;
        newStart = newEnd - totalVisible + 1;
      }
      // Update all chart lines with the same range
      for (const line of this.chartLines)
        line.setVisibleRange(newStart, newEnd);

      this.redraw();
    });
  }

  private enableXAxisZoom(): void {
    if (this.canvas == null)
      return;
    const canvas: HTMLCanvasElement = this.canvas;
    canvas.addEventListener('wheel', (event: WheelEvent) => {
      event.preventDefault();

      if (this.chartLines.length == 0)
        return;

      const firstLine: ChartLine = this.chartLines[0];
      const visibleData: UiChartPoint[][] = firstLine.getVisibleData();
      if (visibleData.length == 0)
        return;

      const [startIdx, endIdx] = [firstLine.visibleDataStartIdx, firstLine.visibleDataEndIdx]; // get the current Visible Range
      const totalDataLength = firstLine.getDataSets()[0].length;

      // Get mouse position relative to canvas
      const rect: DOMRect = canvas.getBoundingClientRect();
      const mouseX: number = event.clientX - rect.left;

      // Calculate which data index is under the mouse
      const visibleCount: number = endIdx - startIdx + 1;
      this.pixelsPerPeriod = canvas.width / visibleCount;
      const mouseDataIdx: number = startIdx + Math.floor(mouseX / this.pixelsPerPeriod);

      // Determine zoom direction and factor
      const zoomFactor: number = 1.1; // 10% zoom per wheel step
      const zoomIn: boolean = event.deltaY < 0;

      // Calculate new visible count
      let newVisibleCount: number;
      if (zoomIn)
        newVisibleCount = Math.max(2, Math.floor(visibleCount / zoomFactor)); // shrink visible window
      else
        newVisibleCount = Math.min(totalDataLength, Math.ceil(visibleCount * zoomFactor)); // expand visible window

      // Ensure we don't zoom beyond data bounds and count stays within [2, totalDataLength]
      newVisibleCount = Math.max(2, Math.min(totalDataLength, newVisibleCount));

      // Calculate new start index to keep mouse position fixed
      const mousePositionRatio: number = mouseX / canvas.width;
      let newStart: number = Math.floor(mouseDataIdx - mousePositionRatio * newVisibleCount);
      let newEnd: number = newStart + newVisibleCount - 1;

      // Reset newStart and newEnd so the view always stays within dataset range
      if (newStart < 0) {
        newStart = 0;
        newEnd = Math.min(totalDataLength - 1, newStart + newVisibleCount - 1);
      }

      if (newEnd >= totalDataLength) {
        newEnd = totalDataLength - 1;
        newStart = Math.max(0, newEnd - newVisibleCount + 1);
      }

      newVisibleCount = newEnd - newStart + 1; // Recalculate actual visible count
      this.pixelsPerPeriod = canvas.width / newVisibleCount; // Update pixelsPerPeriod

      // Apply new visible range to all chart lines
      for (const line of this.chartLines)
        line.setVisibleRange(newStart, newEnd);

      this.redraw();
    });
  }
}