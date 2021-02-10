import './../css/main.css';

const divMessages: HTMLDivElement | null = document.querySelector('#divMessages');
const tbMessage: HTMLInputElement | null = document.querySelector('#tbMessage');
const btnSend: HTMLButtonElement | null = document.querySelector('#btnSend');

var scheme = document.location.protocol === "https:" ? "wss" : "ws";
var port = document.location.port ? (":" + document.location.port) : "";
var connectionUrl = scheme + "://" + document.location.hostname + port + "/ws/ExSvPush";

var baseTime = new Date().getTime();
let socket = new WebSocket(connectionUrl);
console.log('after WebSocket() : ' + (new Date().getTime() - baseTime) + 'ms'); // 1ms
socket.onopen = function (event) {
    console.log('in onopen() : ' + (new Date().getTime() - baseTime) + 'ms');   // first (at server start):47ms, 22-25ms later.
};
socket.onclose = function (event) {
    console.log('in onclose() : ' + (new Date().getTime() - baseTime) + 'ms. Connection closed. Code: ' + event.code + '. Reason: ' + event.reason);
};
socket.onerror = function (event) {
    console.error("WebSocket error observed:", event);
};
socket.onmessage = function (event) {
    console.log('in onmessage() : ' + (new Date().getTime() - baseTime) + 'ms. Data received from server:' + event.data);   // first (at server start):48ms (just 1ms later when connection opened), 23-26ms later.

    const semicolonInd = event.data.indexOf(':');
    const msgCode = event.data.slice(0, semicolonInd);
    const msgObjStr = event.data.substring(semicolonInd + 1);
    if (msgCode == 'priceQuoteFromServerCode') {
        if (spanStream != null) {
            spanStream.innerHTML = `<span>${msgObjStr}</span>`;      // this is not really single quote('), but (`), which allows C# like string interpolation.
        }
    } else {

        if (divMessages != null) {
            const m = document.createElement('div');
            m.innerHTML = `<div class="message-author">${msgCode}</div><div>${msgObjStr}</div>`;
            divMessages.appendChild(m);
            divMessages.scrollTop = divMessages.scrollHeight;
        }
    }
};

if (tbMessage != null) {
    tbMessage.addEventListener('keyup', (e: KeyboardEvent) => {
        if (e.key === 'Enter') {
            send();
        }
    });
}

if (btnSend != null) {
    btnSend.addEventListener('click', send);
}

function send() {
    if (tbMessage != null) {
        if (socket != null && socket.readyState === WebSocket.OPEN) {
            console.log('Sending message to server.');
            socket.send('clientMsgCode0:' + tbMessage.value);
        }
    }
}

// SqCore example
const btnStream: HTMLButtonElement | null = document.querySelector('#btnStartStreaming');
const spanStream: HTMLSpanElement | null = document.querySelector('#divStreaming');

if (btnStream != null) {
    btnStream.addEventListener('click', startStream);
}

function startStream() {
    if (socket != null && socket.readyState === WebSocket.OPEN) {
        console.log('Sending startStream message to server.');
        socket.send('startStreamingCode:' + "someParams");
    }
}



