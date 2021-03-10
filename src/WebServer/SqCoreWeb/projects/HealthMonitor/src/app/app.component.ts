import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

declare var gSqUserEmail: string;

// for Dates, JS only understand numbers, so it cannot Parse '2015-11-16T...' to Date. So, it keeps it as string, so I keep it as String here,
// var jsonObject = JSON.parse('{"date":1251877601000}');
// new Date(1293034567877);
// but I have a Date ojbect under it in which I manually set it up properly
export interface HMData {
    AppOk: string;
    StartDate: string;        // JS jannot JSON.Parse proper string dates
    StartDateLoc: Date;
    StartDateTimeSpanStr: string;
    DailyEmailReportEnabled: boolean;

    RtpsOk: string;
    RtpsTimerEnabled: boolean;
    RtpsTimerFrequencyMinutes: number;
    RtpsDownloads: string[];

    VBrokerOk: string;
    ProcessingVBrokerMessagesEnabled: boolean;
    VBrokerReports: string[];
    VBrokerDetailedReports: string[];

    CommandToBackEnd: string;       // "OnlyGetData", "ApplyTheDifferences"
    ResponseToFrontEnd: string;     // it is "OK" or the Error message
}

var gDefaultHMData: HMData = {
    AppOk: "OK",
    StartDate: '1998-11-16T00:00:00',
    StartDateLoc: new Date('1998-11-16T00:00:00'),
    StartDateTimeSpanStr: '',
    DailyEmailReportEnabled: false,

    RtpsOk: 'OK',
    RtpsTimerEnabled: false,
    RtpsTimerFrequencyMinutes: -999,
    RtpsDownloads: ['a', 'b'],

    VBrokerOk: 'OK',
    ProcessingVBrokerMessagesEnabled: false,
    VBrokerReports: ['a', 'b'],
    VBrokerDetailedReports: ['a', 'b'],

    CommandToBackEnd: "OnlyGetData",
    ResponseToFrontEnd: "OK"
};

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
    public m_title: string = 'SQ HealthMonitor Dashboard';    // strongly typed variables in TS
    m_http: HttpClient;
    public m_data: HMData = gDefaultHMData;
    m_baseUrl: string;
    public m_userEmail: string = 'Unknown user';

    // debug info here
    m_webAppResponse: string = '';

    constructor(http: HttpClient) {
        this.m_http = http;
        // this.m_baseUrl = window.location.protocol + "//" + window.location.hostname; // "https://127.0.0.1/ws/dashboard" without port number, so it goes directly to port 443, avoiding Angular ngserve Proxy redirection
        this.m_baseUrl = window.location.origin;
        this.getHMData(gDefaultHMData);
        if (typeof gSqUserEmail == "undefined")
            this.m_userEmail = 'undefined@gmail.com';
        else
            this.m_userEmail = gSqUserEmail;
    }

    public getHMData(p_hmDataToSend: HMData) {
        console.log("getHMData().http with Post start");
        this.m_http.post(this.m_baseUrl + '/WebServer/ReportHealthMonitorCurrentStateToDashboardInJSON', p_hmDataToSend).subscribe(result => {
            console.log("getHMData().http.post('ReportHealthMonitorCurrentStateToDashboardInJSON') returned.");
            console.log("getHMData().http post returned answer: " + JSON.stringify(result));
            //var hmData: HMData = <HMData>hmDataReturned; // Typescript cast: remember that this is a compile-time cast, and not a runtime cast.
            var hmData: HMData = result as HMData; // Typescript cast: remember that this is a compile-time cast, and not a runtime cast.
            this.m_data = hmData;

            // Sadly Javascript loves Local time, so work in Local time; easier;
            // 1. StartDate
            this.m_data.StartDateLoc = new Date(hmData.StartDate);  // "2015-12-29 00:49:54.000Z", because of the Z Zero, this UTC string is converted properly to local time
            this.m_data.StartDate = localDateToUtcString_yyyy_mm_dd_hh_mm_ss(this.m_data.StartDateLoc);    // take away the miliseconds from the dateStr
            var localNow = new Date();  // this is local time: <checked>
            //var utcNowGetTime = new Date().getTime();  //getTime() returns the number of seconds in UTC.
            this.m_data.StartDateTimeSpanStr = getTimeSpanStr(this.m_data.StartDateLoc, localNow);

            //this.m_data.ResponseToFrontEnd = "ERROR";

            this.m_data.AppOk = 'OK';
            if (this.m_data.ResponseToFrontEnd.toUpperCase().indexOf('ERROR') >= 0)
                this.m_data.AppOk = 'ERROR';

            this.m_data.RtpsOk = 'OK';
            for (var i in this.m_data.RtpsDownloads) {
                if (this.m_data.RtpsDownloads[i].indexOf('OK') >= 0) {  // if 'OK' is found
                    continue;
                }
                this.m_data.RtpsOk = 'ERROR';
            }

            this.m_data.VBrokerOk = 'OK';
            for (var i in this.m_data.VBrokerReports) {
                if (this.m_data.VBrokerReports[i].indexOf('OK') >= 0) {  // if 'OK' is found
                    continue;
                }
                this.m_data.VBrokerOk = 'ERROR';
            }

            this.m_webAppResponse = JSON.stringify(hmData);
        }, error => {
            console.error('getHMData().There was an error: ' + error)
        });
    }

    setControlValue(controlName : any, value : any) {
        console.log("setControlValue():" + controlName + "/" + value);
        console.log("setControlValue():" + controlName + "/" + value + "/" + this.m_data.DailyEmailReportEnabled);
        if (controlName == 'chkDailyEmail') {
            if (this.m_data.DailyEmailReportEnabled != value) {
                this.m_data.DailyEmailReportEnabled = value;
                this.m_data.CommandToBackEnd = "ApplyTheDifferences";
                this.getHMData(this.m_data);
            }
        } else if (controlName == 'chkRtps') {
            if (this.m_data.RtpsTimerEnabled != value) {
                this.m_data.RtpsTimerEnabled = value;
                this.m_data.CommandToBackEnd = "ApplyTheDifferences";
                this.getHMData(this.m_data);
            }
        } else if (controlName == 'chkVBroker') {
            if (this.m_data.ProcessingVBrokerMessagesEnabled != value) {
                this.m_data.ProcessingVBrokerMessagesEnabled = value;
                this.m_data.CommandToBackEnd = "ApplyTheDifferences";
                this.getHMData(this.m_data);
            }
        }
    }

    refreshDataClicked() {
        console.log("refreshDataClicked");
        this.m_data.CommandToBackEnd = "OnlyGetData";
        this.getHMData(this.m_data);
    }
}




// ************** Utils section

function localDateToUtcString_yyyy_mm_dd_hh_mm_ss(p_date: Date) {
    var year = "" + p_date.getUTCFullYear();
    var month = "" + (p_date.getUTCMonth() + 1); if (month.length == 1) { month = "0" + month; }
    var day = "" + p_date.getUTCDate(); if (day.length == 1) { day = "0" + day; }
    var hour = "" + p_date.getUTCHours(); if (hour.length == 1) { hour = "0" + hour; }
    var minute = "" + p_date.getUTCMinutes(); if (minute.length == 1) { minute = "0" + minute; }
    var second = "" + p_date.getUTCSeconds(); if (second.length == 1) { second = "0" + second; }
    return year + "-" + month + "-" + day + " " + hour + ":" + minute + ":" + second;
}

// Started on 2015-12-23 00:44 (0days 0h 12m ago)
function getTimeSpanStr(date1: Date, date2: Date) {
    var diff = date2.getTime() - date1.getTime();

    var days = Math.floor(diff / (1000 * 60 * 60 * 24));
    diff -= days * (1000 * 60 * 60 * 24);

    var hours = Math.floor(diff / (1000 * 60 * 60));
    diff -= hours * (1000 * 60 * 60);

    var mins = Math.floor(diff / (1000 * 60));
    diff -= mins * (1000 * 60);

    var seconds = Math.floor(diff / (1000));
    diff -= seconds * (1000);

    return "(" + days + "days " + hours + "h " + mins + "m " + seconds + "s ago)";
}