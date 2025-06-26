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