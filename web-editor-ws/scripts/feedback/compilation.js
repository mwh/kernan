// Handles the feedback for the compilation reporting.

"use strict";

exports.setup = function (compilation, output, onBuild, onRun) {
  var build, dots, header, interval, onStop, run, stop;

  interval = null;

  header = compilation.children("header");
  build = header.children(".build");
  run = header.children(".run");
  stop = header.children(".stop");
  dots = header.find(".dots");

  function reset() {
    if (interval !== null) {
      clearInterval(interval);
      dots.text("...");
      interval = null;
    }

    header
      .removeClass("building")
      .removeClass("ready")
      .removeClass("running");
  }

  onStop = null;

  function building() {
    reset();
    header.addClass("building");

    dots.text("");
    interval = setInterval(function () {
      var text = dots.text();

      dots.text(text === "..." ? "" : text + ".");
    }, 200);
  }

  build.click(function () {
    building();
    onBuild();
  });

  run.click(function () {
    output.clear();
    header.addClass("running");
    onStop = onRun();
  });

  stop.click(function () {
    var stopper = onStop;
    onStop = null;

    header.removeClass("running");

    if (typeof stopper === "function") {
      stopper();
    }
  });

  return {
    "waiting": reset,

    "error": reset,

    "building": building,

    "running": function () {
      header.addClass("running");
    },

    "ready": function () {
      reset();
      header.addClass("ready");
    },

    "stop": function () {
      stop.click();
    }
  };
};
