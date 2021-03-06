﻿var errorDescription = null;
function failureHandler(jqXHR, textStatus, errorThrown)
{
	if(jqXHR.status == 404)
	{
		errorDescription = '<p><strong>Error 404</strong> Not Found.</p>';
	}
	else if(jqXHR.status == 410)
	{
		errorDescription = '<p><strong>Error 410</strong> Gone</p><p>This document has been removed voluntarily by moderators.</p>';
	}
	else if(jqXHR.status == 451)
	{
		errorDescription = '<p><strong>Error 451</strong> Unavailable For Legal Reasons</p><p>This document has been removed <strong>involuntarily</strong> by moderators.</p>';
	}
	else
	{
		errorDescription = '<p><strong>Error ' + jqXHR.status + '</strong> ' + jqXHR.statusText + '</p>';
	}
}
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
        if(errorDescription == null)
        {
	        $("#scaffolding").show();
        }
        else
        {
	        var view = $('#errorView');
			view.show();
			view.html(errorDescription);
        }
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