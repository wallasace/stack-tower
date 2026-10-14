// Abre o link no próximo "soltar" do toque/clique: o navegador aceita porque é um gesto do usuário.
mergeInto(LibraryManager.library, {
  OpenLinkOnNextPointerUp: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var opened = false;
    var handler = function () {
      if (opened) return;
      opened = true;
      document.removeEventListener('pointerup', handler, true);
      document.removeEventListener('touchend', handler, true);
      window.open(url, '_blank', 'noopener');
    };
    document.addEventListener('pointerup', handler, true);
    document.addEventListener('touchend', handler, true);
  }
});
