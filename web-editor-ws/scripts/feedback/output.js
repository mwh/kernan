// The logging and runtime error reporting system.

"use strict";

var $ = require("jquery");

exports.setup = function (output) {
  function scroll() {
    output.scrollTop(0).scrollTop(output.children().last().position().top);
  }

  function newChunk(text) {
    return $("<p>").text(text);
  }

  function newError(text) {
    return $("<p>").addClass("error").html($("<div>").text(text));
  }

  function newTrace() {
    return $("<ol>").addClass("trace");
  }

  function newTraceLine(text) {
    return $("<li>").text(text);
  }

  return {
    "write": function (content) {
      output.append(newChunk(content));
      scroll();
    },

    "clear": function () {
      output.children().remove();
    },

    "error": function (error) {
      var location;

      if (typeof error === "string") {
        output.append(newError(error));
        return;
      }

      if (error.stack !== undefined) {
        location = error.stack;
      } else {
        location = '    in "' + error.module + '"';

        if (error.line !== null) {
          location += " (line " + error.line + ", column " + error.column + ")";
        }
      }

      output.append(newError(error.message)
        .append(newTrace().append(newTraceLine(location))));
    }
  };
};
