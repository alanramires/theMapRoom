mergeInto(LibraryManager.library, {
  SyncFilesToIndexedDB: function () {
    FS.syncfs(false, function (err) {
      if (err) console.warn("[SaveGame] WebGL syncfs error: " + err);
    });
  },
  LoadFilesFromIndexedDB: function (objectName, callbackMethod) {
    var objName = UTF8ToString(objectName);
    var cbMethod = UTF8ToString(callbackMethod);
    FS.syncfs(true, function (err) {
      if (err) console.warn("[SaveGame] WebGL syncfs load error: " + err);
      SendMessage(objName, cbMethod);
    });
  }
});
