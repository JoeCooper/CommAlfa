Array.prototype.firstOrDefault = function(func){
    return this.filter(func)[0] || null;
};
class LoadManager {
	constructor(onStart, onComplete) {
		this.onStart = onStart;
		this.onComplete = onComplete;
		this.state = 0;
	}
	push() {
		this.state = this.state + 1;
		if(this.state == 1) {
			this.onStart();
		}
	}
	pop() {
		this.state = this.state - 1;
		if(this.state == 0) {
			this.onComplete();
		}
	}
}
const mainPageLoad = new LoadManager(
	function () {
        $("#busyIndicator").show();
        $("#scaffolding").hide();
	},
	function () {
        $("#busyIndicator").hide();
        $("#scaffolding").show();
	});
function loadPush() {
	mainPageLoad.push();
}
function loadPop(onComplete = function () {}) {
	mainPageLoad.pop();
	if(mainPageLoad.state == 0) {
	    onComplete();
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