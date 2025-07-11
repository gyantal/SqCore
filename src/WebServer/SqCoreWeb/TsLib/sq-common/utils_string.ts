// Markdown language syntax: (https://www.markdownguide.org/cheat-sheet/)
// Headings: # -> H1, ## -> H2, ### -> H3.............etc,
// Bold: **bold text**
// Italic: *italicized text*
// Ordered List: 1. First item, 2. Second item 3. Third item
// Unordered List: - First item, - Second item, - Third item

export function markdown2HtmlFormatter(markdownText: string): string {
  if (markdownText == null)
    return '';
  let formattedHtml: string = markdownText;
  // Convert headings from H1 to H6
  formattedHtml = formattedHtml.replace(/^###### (.*)$/gm, '<h6>$1</h6>')
      .replace(/^##### (.*)$/gm, '<h5>$1</h5>')
      .replace(/^#### (.*)$/gm, '<h4>$1</h4>')
      .replace(/^### (.*)$/gm, '<h3>$1</h3>')
      .replace(/^## (.*)$/gm, '<h2>$1</h2>')
      .replace(/^# (.*)$/gm, '<h1>$1</h1>');

  // Convert bold (**bold**) and italic (*italic* or _italic_)
  formattedHtml = formattedHtml.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>') // bold
      .replace(/(?:\*|_)([^*_]+)(?:\*|_)/g, '<em>$1</em>'); // italic

  // Convert markdown links
  formattedHtml = formattedHtml.replace(/\[([^\]]+)]\((https?:\/\/[^\s)]+)\)/g,
      '<a href="$2" target="_blank">$1</a>');

  // Convert tables
  formattedHtml = formattedHtml.replace(/(^\|.+\|\n\|[-| ]+\|\n(?:\|.+\|\n?)+)/gm,
      (tableBlock: string) => convertMarkdownTableToHtml(tableBlock.trim())
  );

  // Ordered lists: 1. item1, 2. item2
  formattedHtml = formattedHtml.replace(/(^|\n)((?:\d+\. .+(?:\n|$))+)/g, (_: string, lineBreak: string, listBlock: string) => {
    return `${lineBreak}${createHtmlListStr(listBlock, 'ol')}`;
  });

  // Unordered lists: - item or * item
  formattedHtml = formattedHtml.replace(/(^|\n)((?:[-*] (?!\d+\.).+(?:\n|$))+)/g, (_: string, lineBreak: string, listBlock: string) => {
    return `${lineBreak}${createHtmlListStr(listBlock, 'ul')}`;
  });

  return formattedHtml.trim();
}

function createHtmlListStr(listBlock: string, tag: 'ol' | 'ul'): string {
  const rawText: string[] = listBlock.trim().split('\n');
  let listHtmlStr = '';
  for (let i = 0; i < rawText.length; i++) {
    let listItemText = rawText[i].trim();

    // Remove the Markdown bullet or number prefix
    if (tag == 'ul')
      listItemText = listItemText.replace(/^[-*]\s+/, '');
    else if (tag == 'ol')
      listItemText = listItemText.replace(/^\d+\.\s+/, '');

    if (listItemText != '')
      listHtmlStr += `<li>${listItemText}</li>`;
  }
  return `<${tag}>${listHtmlStr}</${tag}>`; // Wrap all <li> items in <ol> or <ul>
}

// Convert markdown table block to HTML table
function convertMarkdownTableToHtml(tableMarkdown: string): string {
  const tableData = tableMarkdown.split('\n');
  if (tableData.length < 2)
    return tableMarkdown;

  const headerLine = tableData[0];
  const headers: string[] = [];
  const headerCells = headerLine.split('|');
  for (let i = 0; i < headerCells.length; i++) {
    const headerContent = headerCells[i].trim();
    if (headerContent !== '')
      headers.push(headerContent);
  }

  // Check if table has a 'Ticker' column
  const tickerColIndex: number = headers.findIndex((header) => header == 'Ticker');

  const tableDataRows: string[][] = [];
  for (let i = 2; i < tableData.length; i++) {
    const rowLine = tableData[i];
    const rowCells = rowLine.split('|');
    const row: string[] = [];
    for (let j = 0; j < rowCells.length; j++) {
      const cellContent = rowCells[j].trim();
      if (cellContent !== '')
        row.push(cellContent);
    }
    tableDataRows.push(row);
  }

  let thead = '<thead><tr>';
  for (let i = 0; i < headers.length; i++)
    thead += `<th>${headers[i]}</th>`;
  thead += '</tr><tr><td colspan="' + headers.length + '"><hr></td></tr></thead>';

  let tbody = '<tbody>';
  for (let i = 0; i < tableDataRows.length; i++) {
    tbody += '<tr>';
    for (let j = 0; j < tableDataRows[i].length; j++) {
      if ( j == tickerColIndex) {
        const ticker = tableDataRows[i][j];
        tbody += `<td><a href="https://sqcore.net/webapps/TechnicalAnalyzer/?tickers=${ticker}" target="_blank">${ticker}</a></td>`;
      } else
        tbody += `<td>${tableDataRows[i][j]}</td>`;
    }
    tbody += '</tr><tr><td colspan="' + headers.length + '"><hr></td></tr>';
  }
  tbody += '</tbody>';

  return `<table>${thead}${tbody}</table>`;
}