"use strict";

var $ = require("jquery");

require("setimmediate");

exports.setup = function (editor, sidebar, resize) {
  var isClicked, min, orig;

  isClicked = false;
  orig = sidebar.width();
  min = parseInt(sidebar.css("min-width"), 10);

  function store() {
    localStorage.sidebarWidth = sidebar.width();
  }

  function update() {
    if (localStorage.sidebarMinned) {
      sidebar.width(min);
    } else {
      sidebar.width(localStorage.sidebarWidth);
    }

    editor.resize();
  }

  function toggle() {
    if (!localStorage.sidebarMinned &&
        parseInt(localStorage.sidebarWidth, 10) === min) {
      localStorage.sidebarWidth = orig;
    }

    if (localStorage.sidebarMinned) {
      delete localStorage.sidebarMinned;
    } else {
      localStorage.sidebarMinned = true;
    }

    update();
  }

  resize.mousedown(function () {
    isClicked = true;
  });

  $(document).mouseup(function () {
    isClicked = false;
  }).mousemove(function (event) {
    if (isClicked) {
      sidebar.width(event.pageX);
      editor.resize();

      // Recalculate the width here to account for min-width;
      store();

      return false;
    }
  }).keypress(function (event) {
    if ((event.which === 6 || event.which === 70) &&
        event.shiftKey && (event.ctrlKey || event.metaKey)) {
      toggle();
    }
  });

  if (localStorage.sidebarWidth) {
    update();
  } else {
    store();
  }
};
