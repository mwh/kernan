"use strict";

var $, path, queue, worker;

$ = require("jquery");
path = require("path");

queue = {};
//worker = new Worker("scripts/background.js");

function pump(name, key, value) {
  var i, l, q;

  q = queue[name] || [];

  for (i = 0, l = q.length; i < l; i += 1) {
    q[i][key](value);
  }

  delete queue[name];
}

function isCompiled(name) {
  return global.hasOwnProperty("gracecode_" + path.basename(name, ".grace"));
}

exports.isCompiled = isCompiled;

exports.isCompiling = function (name) {
  name = path.basename(name, ".grace");

  return queue[name] && queue[name].length > 0;
};

exports.forget = function (name) {
  name = path.basename(name, ".grace");
  delete global["gracecode_" + name];

  //worker.postMessage({
  //  "action": "forget",
  //  "name": name
  //});
};

function compile(name, source, callback) {

  var callbacks = queue[name] || [];
  callbacks.push({
    "onSuccess": function (output) {
      callback(null, output);
    },
    "onFailure": callback
  });
  if (!queue.hasOwnProperty(name)) {
    queue[name] = callbacks;
  }
  minigrace.build(name, source);

}

exports.compile = compile;

minigrace.onmessages.push(function(result) {
    if (result.mode == 'static-error') {
        pump(result.module, "onFailure", result);
    }
    if (result.event == 'build-succeeded') {
        pump(result.key, "onSuccess", "");
    }
});

worker = {};
worker.onmessage = function (event) {
  var count, match, output, recompile, regexp, result;

  result = event.data;

  function respond(error) {
    if (count === 0) {
      return;
    }

    if (error !== null) {
      count = 0;
      pump(result.name, "onFailure", error);
    } else {
      count -= 1;

      if (count === 0) {
        pump(result.name, "onSuccess", result.output);
      }
    }
  }

  if (result.isSuccessful) {
    output = result.output;
    regexp = /do_import\("(\w+)", \w+\)/;

    match = output.match(regexp);

    if (match !== null) {
      recompile = [];

      do {
        if (!isCompiled(match[1])) {
          if (!localStorage.hasOwnProperty("file:" + match[1] + ".grace")) {
            pump(result.name, "onFailure", {
              "message": 'Cannot find module "' + match[1] + '"'
            });

            return;
          }

          recompile.push(match[1]);
        }

        output = output.substring(match.index + match[0].length);
        match = output.match(regexp);
      } while (match !== null);

      if (recompile.length > 0) {
        count = recompile.length;
        recompile.forEach(function (name) {
          compile(name, localStorage["file:" + name + ".grace"], respond);
        });

        return;
      }
    }

    pump(result.name, "onSuccess", result.output);
  } else if (result.dependency) {
    if (queue[result.dependency]) {
      worker.postMessage({
        "action": "compile",
        "name": result.name
      });
    } else if (localStorage.hasOwnProperty("file:" +
        result.dependency + ".grace")) {
      compile(result.dependency,
        localStorage["file:" + result.dependency + ".grace"], function (error) {
          if (error !== null) {
            pump(result.name, "onFailure", error);
          } else {
            worker.postMessage({
              "action": "compile",
              "name": result.name
            });
          }
        });
    } else {
      pump(result.name, "onFailure", {
        "message": 'Cannot find module "' + result.dependency + '"'
      });
    }
  } else {
    pump(result.name, "onFailure", result.reason);
  }
};

$(function () {
 // $("#version").text(MiniGrace.version);
});
