// d3.selectAll("#my_dataviz > *").remove();
//      var margin = {top: 10, right: 30, bottom: 30, left: 60 };
//      var width = 660 - margin.left - margin.right;
//      var height = 400 - margin.top - margin.bottom;
 
//      var histChrtSvg = d3.select('#my_dataviz').append('svg')
//                   .attr("width", width + margin.left + margin.right)
//                   .attr("height", height + margin.top + margin.bottom)
//                   .append('g')
//                   .attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');
 
//      uiHistData[0].brAccChrtActuals.map((d: {chartDate: string | number | Date; chrtSdaClose: string | number; }) => 
//              ({
//                chartDate: new Date(d.chartDate),
//                chrtSdaClose: +d.chrtSdaClose,
//              }));
//     uiHistData[1].brAccChrtActuals.map((d: {chartDate: string | number | Date; chrtSdaClose: string | number; }) => 
//              ({
//                chartDate: new Date(d.chartDate),
//                chrtSdaClose: +d.chrtSdaClose,
//              }));
 
//      const formatMonth = d3.timeFormat("%Y%m%d");
//      var  bisectDate = d3.bisector((d: any) => d.chartDate).left;
//      // find data range
//      var xMin = d3.min(uiHistData[0].brAccChrtActuals, (d:{ chartDate: any; }) => d.chartDate);
//      var xMax = d3.max(uiHistData[0].brAccChrtActuals, (d:{ chartDate: any; }) => d.chartDate);
//      var yMin = d3.min(uiHistData[0].brAccChrtActuals, (d: { chrtSdaClose: any; }) => d.chrtSdaClose );
//      var yMax = d3.max(uiHistData[0].brAccChrtActuals, (d: { chrtSdaClose: any; }) => d.chrtSdaClose );
//      var yMin2 = d3.min(uiHistData[1].brAccChrtActuals, (d: { chrtSdaClose: any; }) => d.chrtSdaClose );
//      var yMax2 = d3.max(uiHistData[1].brAccChrtActuals, (d: { chrtSdaClose: any; }) => d.chrtSdaClose );
 
//               // range of data configuring
//      var histChrtScaleX = d3.scaleTime()
//                .domain([xMin, xMax])
//                .range([0, width]);
//      var histChrtScaleY = d3.scaleLinear()
//                  .domain([yMin-5, yMax])
//                  .range([height, 0]);
//      var histChrtScaleY2 = d3.scaleLinear()
//                  .domain([yMin2, yMax2])
//                  .range([height, 0]);
//      histChrtSvg.append('g')
//                 .attr('transform', 'translate(0,' + height + ')')
//                 .call(d3.axisBottom(histChrtScaleX));
 
//      histChrtSvg.append('g')
//                 .call(d3.axisLeft(histChrtScaleY));
//                 histChrtSvg.append('g')
//                 .attr("transform", "translate(" + width + " ,0)")	
//                 .call(d3.axisRight(histChrtScaleY2));
//      // text label for x-axis
//      histChrtSvg.append("text")
//                 .attr("x", width/2)
//                 .attr("y", height + margin.bottom) 
//                 .style("text-anchor","middle")
//                 .text("Date");
//      // text label for y-axis primary
//      histChrtSvg.append("text")
//                 .attr("transform", "rotate(-90)")
//                 .attr("y", 0-margin.left)
//                 .attr("x", 0-(height/2))
//                 .attr("dy","1em")
//                 .style("text-anchor", "middle")
//                 .text("sdaClose(K)");
//       histChrtSvg.append("text")
//                 .attr('transform', 'translate(' + width + ', 0)')
//                 .attr("y", 0-margin.left)
//                 .attr("x", 0-(height/2))
//                 .attr("dy","1em")
//                 .style("text-anchor", "middle")
//                 .text("sdaClose(K)");
//      // Create the circle that travels along the curve of chart
//       var focus = histChrtSvg.append('g')
//                             .append('circle')
//                             .style("fill", "none")
//                             .attr("stroke", "black")
//                             .attr('r', 5)
//                             .style("opacity", 0);
//      // Create the text that travels along the curve of chart
//       var focusText = histChrtSvg.append('g')
//                                 .append('text')
//                                 .style("opacity", 0)
//                                 .attr("text-anchor", "left")
//                                 .attr("alignment-baseline", "middle");
//      // Genereating line - for sdaCloses 
//       var line = d3.line()
//                    .x( (d: any) => histChrtScaleX(d.chartDate))
//                    .y( (d: any) => histChrtScaleY(d.chrtSdaClose))
//                    .curve(d3.curveCardinal);
//       var line2 = d3.line()
//                     .x( (d: any) => histChrtScaleX(d.chartDate))
//                     .y( (d: any) => histChrtScaleY2(d.chrtSdaClose))
//                     .curve(d3.curveCardinal);
//       histChrtSvg.append('path')
//                  .attr('class', 'line') //Assign a class for styling
//                  .datum(uiHistData[0].brAccChrtActuals) // Binds data to the line
//                  .attr('d', line as any);
//       histChrtSvg.append('path')
//                  .attr('class', 'line2') //Assign a class for styling
//                  .datum(uiHistData[1].brAccChrtActuals) // Binds data to the line2
//                  .attr('d', line2 as any);
//       histChrtSvg.append('rect')
//                  .style("fill", "none")
//                  .style("pointer-events", "all")
//                  .attr('width', width)
//                  .attr('height', height)
//                  .on('mouseover', mouseover)
//                  .on('mousemove', mousemove)
//                  .on('mouseout', mouseout);
//      function mouseover() {
//        focus.style("opacity", 1)
//        focusText.style("opacity",1)
//      }
//      function mousemove(event: any) {
//         // recover coordinate we need
//        var x0 = histChrtScaleX.invert(d3.pointer(event)[0]);
//        // console.log(`The X0: '${x0}'`);
//        var i = bisectDate(uiHistData[0].brAccChrtActuals, x0, 1), // index value on the chart area
//        selectedData = uiHistData[0].brAccChrtActuals[i],
//        selectedData1 = uiHistData[0]
//        focus.attr("cx",histChrtScaleX(selectedData.chartDate))
//            .attr("cy",histChrtScaleY(selectedData.chrtSdaClose))
//        focusText.html("s:" + selectedData1.sqTicker + " - " + "x:" + formatMonth(selectedData.chartDate) +  " - " + "y:" + (selectedData.chrtSdaClose).toFixed(2))
//                .attr("x", histChrtScaleX(selectedData.chartDate)+15)
//                .attr("y",histChrtScaleY(selectedData.chrtSdaClose))
//      }
//      function mouseout() {
//        focus.style("opacity", 0)
//        focusText.style("opacity", 0)
//      }