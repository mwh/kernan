var minigrace = {};
var sock = new WebSocket("ws://localhost:25447/grace");

sock.onmessage = function(ev) {
    var js = JSON.parse(ev.data);
    if (js.mode == 'output') {
        minigrace.stdout_write(js.output);
        console.log("+" + (new Date() - minigrace.startTime) + ": got output: " + js.output);
    } else if (js.mode == 'runtime-error') {
        minigrace.running = false;
        minigrace.stderr_write(js.output);
        for (var i = 0; i < js.stack.length; i++) {
            minigrace.stderr_write("  From " + js.stack[i]);
        }
    } else if (js.mode == 'static-error') {
        minigrace.running = false;
    } else if (js.event == 'execution-complete') {
        minigrace.running = false;
        var runtime = new Date() - minigrace.startTime;
        console.log(js.key + " completed in " + runtime + "ms.");
        if (minigrace.onExecutionComplete != null)
            minigrace.onExecutionComplete.call(null);
    } else if (js.mode == 'call') {
        remote_call(js);
    }
    for (var i = 0; i < minigrace.onmessages.length; i++) {
        minigrace.onmessages[i](js);
    }
}

minigrace.running = false;

minigrace.run = function() {
    minigrace.running = true;
    minigrace.startTime = new Date();
    sock.send(JSON.stringify(
                {
                    mode: 'run',
                    code: minigrace.lastSourceCode,
                    modulename: minigrace.lastModname
                }
                )
            );
}

minigrace.build = function(name, source) {
    minigrace.startTime = new Date();
    sock.send(JSON.stringify(
                {
                    mode: 'build',
                    code: source,
                    modulename: name
                }
                )
            );
}

minigrace.stop = function() {
    if (!minigrace.running)
        return;
    sock.send(JSON.stringify(
                {
                    mode: 'stop',
                }
                )
            );
}

minigrace.stdout_write = function() {}
minigrace.stderr_write = function() {}

minigrace.onmessages = [];
minigrace.onExecutionComplete = null;

var mapped_objects = [window, document];
var object_id_map = new WeakMap();

function get_mapped_object(key) {
    if (key == 'window')
        return window;
    else if (key == 'document')
        return document;
    else
        return mapped_objects[key];
}

function create_callback_function(id) {
    return function() {
        sock.send(JSON.stringify(
                    {
                        'mode': 'callback',
                        'block': id,
                        'args': map_reply_array(arguments)
                    }
                    )
                );
    };
}

function map_args(args) {
    var ret = [];
    for (var i = 0; i < args.length; i++) {
        var a = args[i];
        if (typeof a == 'object') {
            if (typeof a.callback != 'undefined') {
                var f = create_callback_function(a.callback);
                ret.push(f);
            } else {
                ret.push(get_mapped_object(a.key));
            }
        } else
            ret.push(a);
    }
    return ret;
}

function remote_assign(rec, call) {
    var name = call.name.substring(0, call.name.length - 3);
    rec[name] = map_args(call.args)[0];
    remote_reply(call, undefined);
}

var theWindow = null;

function close_stop() {
    minigrace.stop();
    if (minigrace.onExecutionComplete != null)
        minigrace.onExecutionComplete.call(null);
}

function protocol_call(call) {
    if (call.name == 'init') {
        if (theWindow !== null) {
            if (theWindow.removeEventListener)
                theWindow.removeEventListener('unload', close_stop);
            theWindow.close();
        }
        var win = window.open(
                'about:blank',
                '_blank',
                'width=540,height=500,status=no,scrollbars=no,toolbar=no');
        if (win == null) {
            minigrace.stop();
            if (minigrace.onExecutionComplete != null)
                minigrace.onExecutionComplete.call(null);
            return;
        }
        theWindow = win;
        // about:blank doesn't get an onload execution on Chrome, so
        // we fall back to a timer and assume it didn't take more than
        // half a second there.
        var timerID;
        var func = function() {
            win.document.title = "Grace";
            remote_object_map(win.document);
            remote_reply(call, win);
            clearTimeout(timerID);
            win.removeEventListener('load', func);
            win.addEventListener('unload', close_stop);
        };
        win.addEventListener('load', func);
        timerID = setTimeout(func, 500);
        mapped_objects = [];
        object_id_map = new WeakMap();
        remote_object_map(win);
    }
}

function remote_call(call) {
    if (call.receiver == -1) {
        return protocol_call(call);
    }
    var rec = get_mapped_object(call.receiver);
    if (call.name.substr(call.name.length - 3) == " :=") {
        return remote_assign(rec, call);
    }
    if (call.name == 'asString') {
        return remote_reply(call, String(rec));
    }
    var obj = rec[call.name];
    var ret;
    if (typeof obj == 'function') {
        var args = map_args(call.args);
        console.log(call.args);
        console.log(args);
        ret = obj.apply(rec, args);
    } else {
        ret = obj;
    }
    remote_reply(call, ret);
}

function remote_object_map(obj) {
    if (object_id_map.has(obj)) {
        return object_id_map.get(obj);
    }
    var idx = mapped_objects.length;
    object_id_map.set(obj, idx);
    mapped_objects.push(obj);
    console.log("mapping " + obj + " at " + idx);
    return idx;
}

function format_reply(resp, ret) {
    if (typeof ret == 'object') {
        resp.object = remote_object_map(ret);
    } else if (typeof ret == 'string') {
        resp.string = ret;
    } else if (typeof ret == 'number') {
        resp.number = ret;
    } else if (typeof ret == 'undefined') {
        resp.done = true;
    }
}

function remote_reply(call, ret) {
    var resp = {
        mode: 'response',
        key: call.key,
    }
    format_reply(resp, ret);
    console.log(resp);
    sock.send(JSON.stringify(resp));
}

function map_reply_array(replies) {
    var arr = [];
    for (var i=0; i<replies.length; i++) {
        var resp = {};
        format_reply(resp, replies[i]);
        arr.push(resp);
    }
    return arr;
}
