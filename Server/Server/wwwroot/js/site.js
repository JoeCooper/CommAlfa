// Write your JavaScript code.
var loadState = 0;
function loadPush() {
    loadState++;
    loadUpdateViews();
}
function loadPop(onComplete = function () {}) {
    loadState--;
    if(loadState == 0) {
	    onComplete();
    }
    loadUpdateViews();
}
function loadUpdateViews() {
    if(loadState > 0) {
        $("#busyIndicator").show();
        $("#scaffolding").hide();
    }
    else {
        $("#busyIndicator").hide();
        $("#scaffolding").show();
    }
}
function escapeHtml(text) {
	var map = {
	'&': '&amp;',
	'<': '&lt;',
	'>': '&gt;',
	'"': '&quot;',
	"'": '&#039;'
	};
	return text.replace(/[&<>"']/g, function(m) { return map[m]; });
}