// Sets up the various parts of the feedback system.

"use strict";

var compilation, output;

compilation = require("./feedback/compilation");
output = require("./feedback/output");

exports.setup = function (feedback, onBuild, onRun) {
  var comp, op;

  op = output.setup(feedback.find(".output"));
  comp = compilation.setup(feedback.find(".compilation"), op, function () {
    op.clear();
    onBuild();
  }, onRun);

  // Stabilise the feedback width so that writing overly long lines to the
  // output does not cause it to wrap.
  feedback.width(feedback.width()).resize(function () {
    feedback.width(null).width(feedback.width());
  });

  return {
    "compilation": comp,
    "output": op,

    "running": function () {
      op.clear();
      comp.running();
    },

    "error": function (reason, gotoLine) {
      comp.waiting();
      op.error(reason, gotoLine);
    }
  };
};
