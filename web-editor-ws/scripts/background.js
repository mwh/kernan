// Minigrace generates code that relies on the window variable containing the
// global object. While the global object in this runtime doesn't have
// many of the expected features of the standard window, assigning it to a
// global window object suffices to allow the code to compile.
this.window = this;

(function (window) {
  "use strict";

  var sources;

  window.importScripts("minigrace.js");

  sources = {};

  window.minigrace.debugMode = true;
  window.minigrace.printStackFrames = false;
  window.minigrace.verbose = false;

  window.Grace_print = function () {
    return window.var_done;
  };

  function compile(name, source) {
    var dialect, escaped, output, stop;

    stop = false;

    window.minigrace.stderr_write = function (message) {
      var match;

      if (!stop && message.substring(0, 10) !== "minigrace:") {
        message = message.split("\n")[0];
        match = message.match(/\[(\d+):(?:\(?)(\d+)((?:-\d+)?)(?:\)?)\]/);

        window.postMessage({
          "isSuccessful": false,
          "name": name,
          "match": message,
          "reason": {
            "module": name,
            "line": match && match[1],
            "column": match && match[2],
            "message": match ?
              message.substring(message.indexOf(" ") + 1) : message
          }
        });

        stop = true;
      }
    };

    window.minigrace.modname = name;
    window.minigrace.mode = "js";

    try {
      window.minigrace.compile(source);
    } catch (error) {
      if (error instanceof ReferenceError) {
        dialect = error.message.match(/^gracecode_(\w+)/);

        if (dialect !== null) {
          window.postMessage({
            "name": name,
            "isSuccessful": false,
            "dependency": dialect[1]
          });

          return;
        }
      }

      window.postMessage({
        "isSuccessful": false,
        "name": name,
        "reason": {
          "message": error.message,
          "stack": error.stack
        }
      });

      return;
    }

    if (!window.minigrace.compileError) {
      escaped = "gracecode_" + name.replace("/", "$");
      output = window.minigrace.generated_output;

      window["eval"]("var myframe;" + output +
                     ";window." + escaped + "=" + escaped);

      window.postMessage({
        "isSuccessful": true,
        "name": name,
        "output": output
      });
    }
  }

  window.onmessage = function (event) {
    var command = event.data;

    if (command.action === "compile") {
      if (command.hasOwnProperty("source")) {
        sources[command.name] = command.source;
      }

      compile(command.name, sources[command.name]);
    } else if (command.action === "forget") {
      delete window["gracecode_" + command.name];
    }
  };

}(this));
