/* SpecScribe line-aware highlight driver (appended to the vendored Prism bundle by build.js).
   Prism runs in manual mode; on load we highlight each in-portal code block WITHOUT letting Prism rewrite the
   element (which would drop the server-rendered per-line anchors). We tokenize the full text once (so multi-line
   constructs colour correctly), split the highlighted HTML at line boundaries — re-closing/re-opening any spans
   that straddle a newline — and inject each line's fragment into its existing `.code-line` span. The id="L{n}"
   anchors, data-ln gutter, and the no-JS fallback are all preserved. */
(function () {
  'use strict';
  var Prism = window.Prism;
  if (!Prism || !Prism.languages) return;

  // Split a Prism-highlighted HTML string into one entry per source line, keeping any span that crosses a
  // newline valid on both lines (close the open stack at the line break, re-open it on the next).
  function splitLines(html) {
    var lines = [];
    var open = [];      // raw opening tags currently in scope
    var cur = '';
    var i = 0;
    var n = html.length;
    while (i < n) {
      var c = html.charAt(i);
      if (c === '<') {
        var gt = html.indexOf('>', i);
        if (gt === -1) { cur += html.slice(i); break; }
        var tag = html.slice(i, gt + 1);
        cur += tag;
        if (tag.charAt(1) === '/') { open.pop(); }
        else if (tag.charAt(tag.length - 2) !== '/') { open.push(tag); }
        i = gt + 1;
      } else if (c === '\n') {
        var closed = cur;
        for (var k = open.length - 1; k >= 0; k--) closed += '</span>';
        lines.push(closed);
        cur = open.join('');
        i++;
      } else {
        cur += c;
        i++;
      }
    }
    var last = cur;
    for (var j = open.length - 1; j >= 0; j--) last += '</span>';
    lines.push(last);
    return lines;
  }

  function highlight(code) {
    var m = /(?:^|\s)language-([\w-]+)(?:\s|$)/.exec(code.className);
    var grammar = m && Prism.languages[m[1]];
    if (!grammar) return; // unknown language: leave the pre-rendered plain source as-is
    var lineEls = code.querySelectorAll('.code-line');
    if (!lineEls.length) return;
    var text = [];
    for (var i = 0; i < lineEls.length; i++) text.push(lineEls[i].textContent);
    var out = splitLines(Prism.highlight(text.join('\n'), grammar, m[1]));
    for (var j = 0; j < lineEls.length; j++) {
      if (out[j] != null) lineEls[j].innerHTML = out[j];
    }
    code.classList.add('code-highlighted');
  }

  function run() {
    var blocks = document.querySelectorAll('pre.code-file > code[class*="language-"]');
    for (var i = 0; i < blocks.length; i++) highlight(blocks[i]);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
