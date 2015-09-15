"use strict";

var $, path;

$ = require("jquery");
path = require("path");

require("jquery-ui");
require("setimmediate");

exports.setup = function (tree) {
  var current, currentDirectory, dropDirectory, input,
      lastSelect, newFile, onOpenCallbacks, upload;

  current = null;

  input = $("#upload-input");
  upload = $("#upload");
  newFile = $("#new-file");

  onOpenCallbacks = [];

  function isText(name) {
    var ext = path.extname(name);

    return ext === "" ||
    ext === ".grace" || ext === ".txt" || ext === ".json" ||
    ext === ".xml" || ext === ".js" || ext === ".html" || ext === ".xhtml";
  }

  function isImage(name) {
    var ext = path.extname(name);

    return ext === ".jpg" || ext === ".jpeg" ||
           ext === ".bmp" || ext === ".gif" || ext === ".png";
  }

  function isAudio(name) {
    var ext = path.extname(name);

    return ext === ".mp3" || ext === ".ogg" || ext === ".wav";
  }

  function mediaType(name) {
    var ext = path.extname(name);

    return ext === ".mp3" ? "audio/mpeg" :
           ext === ".ogg" ? "audio/ogg" : ext === ".wav" ? "audio/wav" : "";
  }

  function validateName(givenName, category) {
    if (givenName[0] === ".") {
      alert("Names must not begin with a dot.");
      return false;
    }

    if (!/^[\w.]+$/.test(givenName)) {
      alert("Only letters, dots, numbers, and underscores are allowed.");
      return false;
    }

    if (currentDirectory !== undefined) {
      givenName = currentDirectory.attr("dire-name") + "/" + givenName;
    }

    if (localStorage.hasOwnProperty(category + ":" + givenName)) {
      alert("That name is already taken.");
      return false;
    }

    return true;
  }

  function getName(lastName, category) {
    var catName = prompt("Name of " + category + ":");

    if (catName !== null && catName.length > 0) {
      if (path.extname(catName) === "") {
        catName += path.extname(lastName);
      }

      if (!validateName(catName, category)) {
        return getName(catName, category);
      }

      return catName;
    }

    return false;
  }

  function contents(fileName) {
    if (!localStorage.hasOwnProperty("file:" + fileName)) {
      throw new Error("No such file " + fileName);
    }

    return localStorage["file:" + fileName];
  }

  function onOpen(callback) {
    onOpenCallbacks.push(callback);
  }

  function openFile(fileName) {
    var audioTag, content, directory, imageTag, noChange, slashIndex;

    if (!localStorage.hasOwnProperty("file:" + fileName)) {
      throw new Error("Open of unknown file " + fileName);
    }

    noChange = false;

    if (currentDirectory !== undefined) {

      if (currentDirectory.hasClass("directory")) {

        if (currentDirectory.find("ul").css("display") === "none") {
          slashIndex = fileName.lastIndexOf("/");

          if (slashIndex !== -1) {
            directory = fileName.substring(0, slashIndex);
          }

          if (currentDirectory.attr("dire-name") === directory) {
            noChange = true;
          }
        }
      }
    }

    if (!noChange) {
      if (lastSelect !== undefined) {
        lastSelect.css({ "font-weight": "", "color": "" });
      }

      tree.find('[data-name="' + fileName + '"]').css({
        "font-weight": "bold",
        "color": "#FF0000"
      });

      lastSelect = tree.find('[data-name="' + fileName + '"]');
    }

    slashIndex = fileName.lastIndexOf("/");

    if (slashIndex !== -1) {
      directory = fileName.substring(0, slashIndex);
      currentDirectory = tree.find('[dire-name="' + directory + '"]');
    } else {
      currentDirectory = undefined;
    }

    localStorage.currentFile = fileName;
    content = localStorage["file:" + fileName];

    if (isText(fileName)) {
      $("#image-view").addClass("hidden");
      $("#audio-view").addClass("hidden");

      audioTag = document.querySelector("audio");
      audioTag.src = "";
      audioTag.type = "";

      onOpenCallbacks.forEach(function (callback) {
        callback(fileName, content);
      });
    } else if (isImage(fileName)) {
      $("#grace-view").addClass("hidden");
      $("#audio-view").addClass("hidden");
      $("#image-view").removeClass("hidden");

      audioTag = document.querySelector("audio");
      audioTag.src = "";
      audioTag.type = "";

      imageTag = document.querySelector("img");
      imageTag.src = content;
    } else if (isAudio(fileName)) {
      $("#grace-view").addClass("hidden");
      $("#image-view").addClass("hidden");
      $("#audio-view").removeClass("hidden");

      audioTag = document.querySelector("audio");
      audioTag.src = content;
      audioTag.type = mediaType(fileName);
    }
  }

  function save(content) {
    if (!localStorage.currentFile) {
      throw new Error("Save when no file is open");
    }

    localStorage["file:" + localStorage.currentFile] = content;
  }

  function rename(to) {
    var content, file, newDataName = to;

    file = localStorage.currentFile;

    if (!file) {
      throw new Error("Rename when no file is open");
    }

    if (!to) {
      return;
    }

    if (path.extname(to) === "") {
      to += ".grace";
    }

    if (!validateName(to, "file")) {
      return;
    }

    content = localStorage["file:" + file];
    delete localStorage["file:" + file];

    if (currentDirectory !== undefined) {
      newDataName = currentDirectory.attr("dire-name") + "/" + newDataName;
    }

    localStorage["file:" + newDataName] = content;
    tree.find('[data-name="' + file + '"]').attr("data-name", newDataName);
    tree.find('[data-name="' + newDataName + '"]').find(".file-name").text(to);
    localStorage.currentFile = newDataName;

    openFile(newDataName);
  }

  function remove() {
    var file = localStorage.currentFile;

    if (!file) {
      throw new Error("Remove when no file is open");
    }

    delete localStorage["file:" + file];
    tree.find('[data-name="' + file + '"]').remove();
    delete localStorage.currentFile;
  }

  function isChanged(name, value) {
    if (!localStorage.hasOwnProperty("file:" + name)) {
      throw new Error("Cannot compare change non-existent file " + name);
    }

    return localStorage["file:" + name] !== value;
  }

  function addFile(name) {
    var div, inserted, li, parent, slashIndex;

    li = $("<li>");
    li.addClass("file");
    li.attr("data-name", name);

    div = $("<div>");
    div.addClass("file-name");

    slashIndex = name.lastIndexOf("/");

    if (slashIndex !== -1) {
      name = name.substring(slashIndex + 1);
    }

    div.text(name);
    li.append(div);

    if (path.extname(name) === ".grace") {
      li.addClass("grace");
    }

    inserted = false;

    if (currentDirectory === undefined) {
      parent = tree;
    } else {
      parent = currentDirectory.children().children();
    }

    parent.children().each(function () {
      if ($(this).text() > name && $(this).hasClass("file")) {
        $(this).before(li);
        inserted = true;
        return false;
      }
    });

    if (!inserted) {
      parent.append(li);
    }

    li.draggable({
      "revert": "invalid",
      "scroll": false,
      "helper": "clone",
      "appendTo": "body"
    });

    return li;
  }

  function dropFile(draggedFile, droppedDire) {
    var content, dir, draggedName, droppedName,
        name, slashIndex, storeCurrentDirectory;

    draggedName = draggedFile.attr("data-name");
    name = draggedName;
    slashIndex = draggedName.lastIndexOf("/");

    if (droppedDire !== tree) {
      droppedName = droppedDire.attr("dire-name");

      if (slashIndex !== -1) {
        dir = draggedName.substring(0, slashIndex);
        name = draggedName.substring(slashIndex + 1);
      }

      if (droppedName === dir) {
        return false;
      }
    } else {
      if (slashIndex === -1) {
        return false;
      }

      name = draggedName.substring(slashIndex + 1);
      droppedDire = undefined;
    }

    storeCurrentDirectory = currentDirectory;
    currentDirectory = droppedDire;

    if (!validateName(name, "file")) {
      name = getName(name, "file");

      if (!name) {
        return false;
      }
    }

    if (droppedDire !== undefined) {
      name = droppedName + "/" + name;
    }

    addFile(name);
    currentDirectory = storeCurrentDirectory;

    if (lastSelect.attr("data-name") === draggedName) {
      lastSelect.css({ "font-weight": "", "color": "" });
      tree.find('[data-name="' + name + '"]').css({
        "font-weight": "bold",
        "color": "#FF0000"
      });

      lastSelect = tree.find('[data-name="' + name + '"]');
    }

    content = localStorage["file:" + draggedName];
    delete localStorage["file:" + draggedName];
    tree.find('[data-name="' + draggedName + '"]').remove();
    localStorage["file:" + name] = content;

    return true;
  }


  function addDirectory(name) {
    var div, inserted, li, parent, slashIndex, ul;

    li = $("<li>");
    li.addClass("directory");
    li.attr("dire-name", name);

    div = $("<div>");
    div.addClass("icon");
    div.addClass("close");
    li.append(div);

    div = $("<div>");
    div.addClass("directory-name");

    slashIndex = name.lastIndexOf("/");

    if (slashIndex !== -1) {
      name = name.substring(slashIndex + 1);
    }

    div.text(name);
    ul = $("<ul>");
    ul.css({ "display": "block" });

    div.append(ul);
    li.append(div);

    if (currentDirectory === undefined) {
      parent = tree;
    } else {
      parent = currentDirectory.children().children();
    }

    inserted = false;

    parent.children().each(function () {
      if ($(this).text() > name || $(this).hasClass("file")) {
        $(this).before(li);
        inserted = true;
        return false;
      }
    });

    if (!inserted) {
      parent.append(li);
    }

    li.draggable({
      "revert": "invalid",
      "scroll": false,
      "helper": "clone",
      "appendTo": "body"
    });

    li.droppable({
      "greedy": true,
      "scroll": false,
      "tolerance": "pointer",

      "drop": function (event, ui) {
        if (ui.draggable.hasClass("file")) {
          if (!dropFile(ui.draggable, li)) {
            ui.draggable.draggable("option", "revert", true);
          }
        } else if (ui.draggable.hasClass("directory") &&
                   !dropDirectory(ui.draggable, li)) {
          ui.draggable.draggable("option", "revert", true);
        }
      }
    });

    return li;
  }

  function modifyChildren(draggedDire, newDire) {
    draggedDire.children().children().children().each(function () {

      if ($(this).hasClass("file")) {
        dropFile($(this), newDire);

      } else if ($(this).hasClass("directory")) {
        dropDirectory($(this), newDire);
      }
    });
  }

  // Assigned, rather than declared, to make clear the circular use above.
  dropDirectory = function (draggedDire, droppedDire) {
    var content, dir, display, draggedName, droppedName,
        name, newDire, slashIndex, storeCurrentDirectory;

    draggedName = draggedDire.attr("dire-name");
    name = draggedName;
    slashIndex = draggedName.lastIndexOf("/");

    if (droppedDire !== tree) {
      droppedName = droppedDire.attr("dire-name");

      if (slashIndex !== -1) {
        dir = draggedName.substring(0, slashIndex);
        name = draggedName.substring(slashIndex + 1);
      }

      if (droppedName === dir) {
        return false;
      }

    } else {

      if (slashIndex !== -1) {
        name = draggedName.substring(slashIndex + 1);
      } else {
        return false;
      }

      droppedDire = undefined;
    }

    storeCurrentDirectory = currentDirectory;
    currentDirectory = droppedDire;

    if (!validateName(name, "directory")) {
      name = getName(name, "directory");

      if (!name) {
        return false;
      }
    }

    if (droppedDire !== undefined) {
      name = droppedName + "/" + name;
    }

    newDire = addDirectory(name);
    currentDirectory = storeCurrentDirectory;

    display = draggedDire.find("ul").css("display");
    newDire.children().children("ul").css({ "display": display });

    if (newDire.find("ul").css("display") === "block") {
      newDire.children(".icon").removeClass("close");
      newDire.children(".icon").addClass("open");
    }

    modifyChildren(draggedDire, newDire);

    if (currentDirectory !== undefined) {
      if (currentDirectory.attr("dire-name") === draggedName) {
        lastSelect.css({ "font-weight": "", "color": "" });
        newDire.children().css({ "font-weight": "bold", "color": "#FF0000" });
        newDire.children().find("*").css({ "color": "#000000" });
        newDire.children().find(".file").css({ "font-weight": "normal" });

        currentDirectory = newDire;
        lastSelect = newDire.find("*");
      }
    }

    content = localStorage["directory:" + draggedName];
    delete localStorage["directory:" + draggedName];
    tree.find('[dire-name="' + draggedName + '"]').remove();
    localStorage["directory:" + name] = content;

    return true;
  };

  upload.click(function () {
    input.click();
  });

  input.change(function () {
    var file, fileName, fileNameList, i, l, lastValid;

    function readFileList(currentFileName, currentFile) {
      var reader = new FileReader();

      reader.onload = function (event) {
        var result = event.target.result;

        try {
          localStorage["file:" + currentFileName] = result;
        } catch (err) {
          console.error(err.message);
          return;
        }

        addFile(currentFileName);


        if (lastValid === currentFileName) {
          openFile(currentFileName);
        }
      };

      if (isText(currentFileName)) {
        reader.readAsText(currentFile);
      } else if (isImage(currentFileName) || isAudio(currentFileName)) {
        reader.readAsDataURL(currentFile);
      }
    }

    fileNameList = [];

    for (i = 0, l = this.files.length; i < l; i += 1) {
      file = this.files[i];
      fileName = file.name;

      if (!validateName(fileName, "file")) {
        if (!confirm("Rename the file on upload?")) {
          continue;
        }

        fileName = getName(fileName, "file");

        if (!fileName) {
          continue;
        }
      }

      if (currentDirectory !== undefined) {
        fileName = currentDirectory.attr("dire-name") + "/" + fileName;
      }

      fileNameList[i] = fileName;
    }

    for (i = 0; i < l; i += 1) {
      if (fileNameList[i] !== undefined) {
        readFileList(fileNameList[i], this.files[i]);
        lastValid = fileNameList[i];
      }
    }
  });

  function createFile() {
    var file = prompt("Name of new file:");

    if (file !== null && file.length > 0) {
      if (path.extname(file) === "") {
        file += ".grace";
      }

      if (!validateName(file, "file")) {
        file = getName(file, "file");

        if (!file) {
          return;
        }
      }

      if (currentDirectory !== undefined) {
        file = currentDirectory.attr("dire-name") + "/" + file;
      }

      localStorage["file:" + file] = "";
      addFile(file).click();
    }
  }

  function createDirectory() {
    var directory = prompt("Name of new directory:");

    if (directory !== null && directory.length > 0) {
      if (!validateName(directory, "directory")) {
        directory = getName(directory, "directory");

        if (!directory) {
          return;
        }
      }

      if (currentDirectory !== undefined) {
        directory = currentDirectory.attr("dire-name") + "/" + directory;
      }

      localStorage["directory:" + directory] = "";
      addDirectory(directory).click();
    }
  }

  newFile.click(function () {
    var creation = prompt("New file or New directory?", "directory");

    if (creation !== null && creation.length > 0) {
      if (creation === "file") {
        createFile();
      } else if (creation === "directory") {
        createDirectory();
      } else if (confirm("Only file and directory acceptable") === true) {
        newFile.click();
      }
    }
  });

  tree.on("click", ".directory", function (e) {
    var dir, noChange, slashIndex;

    e.stopPropagation();
    current = $(this);
    noChange = false;

    if (currentDirectory !== undefined) {

      if (currentDirectory.hasClass("directory")) {

        if (currentDirectory.find("ul").css("display") === "none") {
          slashIndex = current.attr("dire-name").lastIndexOf("/");

          if (slashIndex !== -1) {
            dir = current.attr("dire-name").substring(0, slashIndex);
          } else {
            dir = current.attr("dire-name");
          }

          if (currentDirectory.attr("dire-name") === dir) {
            noChange = true;
          }
        }
      }
    }

    if (!noChange) {
      if (lastSelect !== undefined) {
        lastSelect.css({ "font-weight": "", "color": "" });
      }

      current.children().css({ "font-weight": "bold", "color": "#FF0000" });
      current.children().find("*").css({ "color": "#000000" });
      current.children().find(".file").css({ "font-weight": "normal" });

      currentDirectory = current;
      lastSelect = current.find("*");
    }

    if (current.find("ul").css("display") === "none") {
      current.children().children("ul").css({ "display": "block" });
      current.children(".icon").removeClass("close");
      current.children(".icon").addClass("open");

    } else if (current.find("ul").css("display") === "block") {
      current.children().children("ul").css({ "display": "none" });
      current.children(".icon").removeClass("open");
      current.children(".icon").addClass("close");
    }
  });

  tree.on("click", ".file", function (e) {
    e.stopPropagation();
    openFile($(this).attr("data-name"));
  });

  tree.on("click", function () {
    if (lastSelect !== undefined) {
      lastSelect.css({ "font-weight": "", "color": "" });
    }

    currentDirectory = undefined;
  });

  tree.droppable({
    "greedy": true,
    "scroll": false,

    "drop": function (event, ui) {
      if (ui.draggable.hasClass("file")) {

        if (!dropFile(ui.draggable, tree)) {
          ui.draggable.draggable("option", "revert", true);
        }

      } else if (ui.draggable.hasClass("directory")) {

        if (!dropDirectory(ui.draggable, tree)) {
          ui.draggable.draggable("option", "revert", true);
        }
      }
    }
  });

  (function () {
    var name;

    for (name in localStorage) {
      if (localStorage.hasOwnProperty(name) &&
          name.substring(0, 5) === "file:") {
        addFile(name.substring(5));
      }
    }
  }());

  if (localStorage.hasOwnProperty("currentFile")) {
    setImmediate(function () {
      openFile(localStorage.currentFile);
    });
  }

  global.graceHasFile = function (name) {
    return localStorage.hasOwnProperty("file:" + name);
  };

  global.graceReadFile = function (name) {
    var data = localStorage["file:" + name];

    if (!isText(name)) {
      data = atob(data);
    }

    return URL.createObjectURL(new Blob([ data ]));
  };

  return {
    "contents": contents,
    "save": save,
    "rename": rename,
    "remove": remove,
    "onOpen": onOpen,
    "isChanged": isChanged
  };
};
