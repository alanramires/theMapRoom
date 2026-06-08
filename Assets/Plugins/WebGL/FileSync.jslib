mergeInto(LibraryManager.library, {
  SyncFilesToIndexedDB: function (objectName, callbackMethod) {
    var objName = UTF8ToString(objectName);
    var cbMethod = UTF8ToString(callbackMethod);
    FS.syncfs(false, function (err) {
      if (err) {
        console.warn("[SaveGame] WebGL syncfs error: " + err);
        SendMessage(objName, cbMethod, "ERR:" + err);
      } else {
        SendMessage(objName, cbMethod, "OK");
      }
    });
  },
  LoadFilesFromIndexedDB: function (objectName, callbackMethod) {
    var objName = UTF8ToString(objectName);
    var cbMethod = UTF8ToString(callbackMethod);
    FS.syncfs(true, function (err) {
      if (err) {
        console.warn("[SaveGame] WebGL syncfs load error: " + err);
        SendMessage(objName, cbMethod, "ERR:" + err);
      } else {
        SendMessage(objName, cbMethod, "OK");
      }
    });
  }
});
