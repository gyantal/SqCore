import './../css/main.css';

// export {}; // TS convention: To avoid top level duplicate variables, functions. This file should be treated as a module (and have its own scope). A file without any top-level import or export declarations is treated as a script whose contents are available in the global scope.

// 1. Declare some global variables and hook on DOMContentLoaded() and window.onload()
console.log('SqCore: Script BEGIN4');

function simulationDriverFunction() {
    console.log("simulationDriverFunction() BEGIN");

    var simLengthHtmlElement = document.getElementById("idSimLength");
    var simLength = Number(simLengthHtmlElement.value);
    if (simLength > 50) {
        simLength = 50;
        idSimLength.value = 50;
    }

    var cagr = Number(document.getElementById("idCAGR").value);
    var stdDev = Number(document.getElementById("idStdDev").value);
    var nSimulations = Number(document.getElementById("idnSimulations").value);
    var iniDep = Number(document.getElementById("idIniDep").value);
    var addDepFreq = Number(document.getElementById("idAddDepFreq").value);
    var addDepVal = Number(document.getElementById("idAddDepVal").value);
    var addDepEnd = document.getElementById("idAddDepEnd").value ? Number(document.getElementById("idAddDepEnd").value) : simLength * 12;
    var withDrawStart = Number(document.getElementById("idWithDrawStart").value);
    var withDrawFreq = Number(document.getElementById("idWithDrawFreq").value);
    var withDrawPerc = Number(document.getElementById("idWithDrawPerc").value);

    if (addDepFreq == 0) {
        if (addDepVal > 0) {
            addDepFreq = 12;
            idAddDepFreq.value = 12;
        }
        else {
            addDepFreq = 12;
        }
    }

    if (addDepFreq > 60) {
        addDepFreq = 60;
        idAddDepFreq.value = 60;
    }

    if (withDrawStart > 600) {
        withDrawStart = 600;
        idWithDrawStart.value = 600;
    }

    var userErrorHtmlElement = document.getElementById("idUserError");
    var isError = ((withDrawFreq + withDrawPerc + withDrawStart > 0 && withDrawFreq * withDrawPerc * withDrawStart == 0) || withDrawPerc < 0 || withDrawFreq % 1 !== 0 || withDrawFreq < 0 || withDrawStart % 1 !== 0 || withDrawStart < 0 || addDepEnd > simLength * 12 || addDepEnd % 1 !== 0 || addDepEnd < 0 || addDepVal < 0 || addDepFreq % 1 !== 0 || addDepFreq < 0 || iniDep < 0 || stdDev <= 0 || nSimulations < 1 || nSimulations % 1 !== 0 || simLength % 1 !== 0 || isNaN(simLength) || isNaN(cagr) || isNaN(stdDev) || isNaN(nSimulations) || isNaN(iniDep) || isNaN(addDepFreq) || isNaN(addDepVal) || isNaN(addDepEnd) || isNaN(withDrawStart) || isNaN(withDrawFreq) || isNaN(withDrawPerc)) ? true : false;
    if (isError != false) {
        userErrorHtmlElement.innerText = "ERROR in inputs";
        userErrorHtmlElement.style.color = "red";
    } else {
        userErrorHtmlElement.innerText = "";
        userErrorHtmlElement.style.color = "black";

        console.log("simulationDriverFunction() inputs OK.");

        simulateFunction(simLength, cagr, stdDev, nSimulations, iniDep, addDepFreq, addDepVal, addDepEnd, withDrawStart, withDrawFreq, withDrawPerc);
    }


    console.log("simulateFunction() END");
}




function simulateFunction(simLength, cagr, stdDev, nSimulations, iniDep, addDepFreq, addDepVal, addDepEnd, withDrawStart, withDrawFreq, withDrawPerc) {
    console.log("simulateFunction() BEGIN");

    //  TODO: calculate
    var totalDeposit = iniDep + Math.floor(addDepEnd / addDepFreq) * addDepVal;

    var developerInfo = "";

    // TODO: write into developer Info




    // make a standard gaussian variable.
    var standard = lbgaussian(cagr / 100 / 252, stdDev / 100 / Math.sqrt(252));


    var nDays = simLength * 252;

    var dailyRets = new Array(nSimulations);
    for (var i = 0; i < dailyRets.length; i++) {

        var dailyRet = new Array(nDays);
        for (var j = 0; j < dailyRet.length; j++) {
            dailyRet[j] = standard();
        }

        dailyRets[i] = dailyRet;
    }

    var dailyPrices = new Array(nSimulations);
    for (var i = 0; i < dailyPrices.length; i++) {

        var dailyPrice = new Array(nDays);
        dailyPrice[0] = 1 + dailyRets[i][0];
        for (var j = 1; j < dailyPrice.length; j++) {
            dailyPrice[j] = dailyPrice[j - 1] * (1 + dailyRets[i][j]);
        }

        dailyPrices[i] = dailyPrice;
    }


    var dailyAddDep = new Array(nDays);
    for (var i = 0; i < dailyAddDep.length; i++) {
        if (i < addDepEnd * 21 && i % (21 * addDepFreq) == (21 * addDepFreq - 1)) {
            dailyAddDep[i] = addDepVal;
        }
        else {
            dailyAddDep[i] = 0;
        }
    }




    var dailyWithDraw = new Array(nDays);
    for (var i = 0; i < dailyWithDraw.length; i++) {
        if (i >= (withDrawStart * 21 - 1) && i % (21 * withDrawFreq) == (21 * withDrawFreq - 1)) {
            dailyWithDraw[i] = withDrawPerc / 100;
        }
        else {
            dailyWithDraw[i] = 0;
        }
    }



    var dailyNoShs = new Array(nSimulations);
    var dailyWithPVs = new Array(nSimulations);
    for (var i = 0; i < dailyNoShs.length; i++) {

        var dailyNoSh = new Array(nDays);
        var dailyWithPV = new Array(nDays);
        dailyNoSh[0] = iniDep;
        dailyWithPV[0] = 0;
        for (var j = 1; j < dailyNoSh.length; j++) {
            dailyNoSh[j] = dailyNoSh[j - 1] + dailyAddDep[j] / dailyPrices[i][j] - dailyNoSh[j - 1] * dailyWithDraw[j];
            dailyWithPV[j] = dailyWithPV[j - 1] + dailyNoSh[j - 1] * dailyWithDraw[j] * dailyPrices[i][j];
        }

        dailyNoShs[i] = dailyNoSh;
        dailyWithPVs[i] = dailyWithPV;
    }


    var dailyNoShsBH = new Array(nSimulations);
    for (var i = 0; i < dailyNoShsBH.length; i++) {

        var dailyNoShBH = new Array(nDays);
        dailyNoShBH[0] = iniDep;
        for (var j = 1; j < dailyNoShBH.length; j++) {
            dailyNoShBH[j] = dailyNoShBH[j - 1] + dailyAddDep[j] / dailyPrices[i][j];
        }

        dailyNoShsBH[i] = dailyNoShBH;
    }

    var dailyPVs = new Array(nSimulations);
    for (var i = 0; i < dailyPVs.length; i++) {

        var dailyPV = new Array(nDays);
        for (var j = 0; j < dailyPV.length; j++) {
            dailyPV[j] = dailyNoShs[i][j] * dailyPrices[i][j];
        }

        dailyPVs[i] = dailyPV;
    }


    var dailyPVsBH = new Array(nSimulations);
    for (var i = 0; i < dailyPVsBH.length; i++) {

        var dailyPVBH = new Array(nDays);
        for (var j = 0; j < dailyPVBH.length; j++) {
            dailyPVBH[j] = dailyNoShsBH[i][j] * dailyPrices[i][j];
        }

        dailyPVsBH[i] = dailyPVBH;
    }


    resultFunction(simLength, nDays, nSimulations, dailyPVs, dailyPVsBH, dailyWithPVs, totalDeposit, developerInfo);
}

function resultFunction(simLength, nDays, nSimulations, dailyPVs, dailyPVsBH, dailyWithPVs, totalDeposit, developerInfo) {

    var pvAvgWoWith = new Array(nDays);
    var pvMedWoWith = new Array(nDays);
    for (var i = 0; i < pvAvgWoWith.length; i++) {
        var subPVAvgWoWith = new Array(nSimulations);
        for (var j = 0; j < subPVAvgWoWith.length; j++) {
            subPVAvgWoWith[j] = dailyPVsBH[j][i];
        }

        pvAvgWoWith[i] = lbaverage(subPVAvgWoWith);
        pvMedWoWith[i] = lbmedian(subPVAvgWoWith);
    }


    var pvRndWoWith = dailyPVsBH[0];


    var pvAvgWith = new Array(nDays);
    var pvMedWith = new Array(nDays);
    for (var i = 0; i < pvAvgWith.length; i++) {
        var subPVAvgWith = new Array(nSimulations);
        for (var j = 0; j < subPVAvgWith.length; j++) {
            subPVAvgWith[j] = dailyPVs[j][i];
        }

        pvAvgWith[i] = lbaverage(subPVAvgWith);
        pvMedWith[i] = lbmedian(subPVAvgWith);
    }


    var pvRndWith = dailyPVs[0];



    var pvAvgWithDrawed = new Array(nDays);
    var pvMedWithDrawed = new Array(nDays);
    for (var i = 0; i < pvAvgWithDrawed.length; i++) {
        var subPVAvgWithDrawed = new Array(nSimulations);
        for (var j = 0; j < subPVAvgWithDrawed.length; j++) {
            subPVAvgWithDrawed[j] = dailyWithPVs[j][i];
        }

        pvAvgWithDrawed[i] = lbaverage(subPVAvgWithDrawed);
        pvMedWithDrawed[i] = lbmedian(subPVAvgWithDrawed);
    }


    var pvRndWithDrawed = dailyWithPVs[0];


    var pvAvgTotal = new Array(nDays);
    var pvMedTotal = new Array(nDays);
    var pvRndTotal = new Array(nDays);
    for (var i = 0; i < pvAvgTotal.length; i++) {
        pvAvgTotal[i] = pvAvgWith[i] + pvAvgWithDrawed[i];
        pvMedTotal[i] = pvMedWith[i] + pvMedWithDrawed[i];
        pvRndTotal[i] = pvRndWith[i] + pvRndWithDrawed[i];
    }


    var profitDollAvgWoWith = pvAvgWoWith[nDays - 1] - totalDeposit;
    var profitPercAvgWoWith = pvAvgWoWith[nDays - 1] / totalDeposit - 1;
    var cagrAvgWoWith = Math.pow((pvAvgWoWith[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollMedWoWith = pvMedWoWith[nDays - 1] - totalDeposit;
    var profitPercMedWoWith = pvMedWoWith[nDays - 1] / totalDeposit - 1;
    var cagrMedWoWith = Math.pow((pvMedWoWith[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollRndWoWith = pvRndWoWith[nDays - 1] - totalDeposit;
    var profitPercRndWoWith = pvRndWoWith[nDays - 1] / totalDeposit - 1;
    var cagrRndWoWith = Math.pow((pvRndWoWith[nDays - 1] / totalDeposit), 1 / simLength) - 1;


    var profitDollAvgWith = pvAvgWith[nDays - 1] - totalDeposit;
    var profitPercAvgWith = pvAvgWith[nDays - 1] / totalDeposit - 1;
    var cagrAvgWith = Math.pow((pvAvgWith[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollMedWith = pvMedWith[nDays - 1] - totalDeposit;
    var profitPercMedWith = pvMedWith[nDays - 1] / totalDeposit - 1;
    var cagrMedWith = Math.pow((pvMedWith[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollRndWith = pvRndWith[nDays - 1] - totalDeposit;
    var profitPercRndWith = pvRndWith[nDays - 1] / totalDeposit - 1;
    var cagrRndWith = Math.pow((pvRndWith[nDays - 1] / totalDeposit), 1 / simLength) - 1;


    var profitDollAvgWithDrawed = pvAvgWithDrawed[nDays - 1];
    var profitPercAvgWithDrawed = pvAvgWithDrawed[nDays - 1] / totalDeposit;
    var cagrAvgWithDrawed = Math.pow(1 + (pvAvgWithDrawed[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollMedWithDrawed = pvMedWithDrawed[nDays - 1];
    var profitPercMedWithDrawed = pvMedWithDrawed[nDays - 1] / totalDeposit;
    var cagrMedWithDrawed = Math.pow(1 + (pvMedWithDrawed[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollRndWithDrawed = pvRndWithDrawed[nDays - 1];
    var profitPercRndWithDrawed = pvRndWithDrawed[nDays - 1] / totalDeposit;
    var cagrRndWithDrawed = Math.pow(1 + (pvRndWithDrawed[nDays - 1] / totalDeposit), 1 / simLength) - 1;


    var profitDollAvgTotal = pvAvgTotal[nDays - 1] - totalDeposit;
    var profitPercAvgTotal = pvAvgTotal[nDays - 1] / totalDeposit - 1;
    var cagrAvgTotal = Math.pow((pvAvgTotal[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollMedTotal = pvMedTotal[nDays - 1] - totalDeposit;
    var profitPercMedTotal = pvMedTotal[nDays - 1] / totalDeposit - 1;
    var cagrMedTotal = Math.pow((pvMedTotal[nDays - 1] / totalDeposit), 1 / simLength) - 1;

    var profitDollRndTotal = pvRndTotal[nDays - 1] - totalDeposit;
    var profitPercRndTotal = pvRndTotal[nDays - 1] / totalDeposit - 1;
    var cagrRndTotal = Math.pow((pvRndTotal[nDays - 1] / totalDeposit), 1 / simLength) - 1;


    var proba = [1, 1, 1, 1, 1, 1];
    var averagePVWoWith = lbstdDev(proba);



    writeOutput(totalDeposit, developerInfo, averagePVWoWith, profitDollAvgWoWith, profitPercAvgWoWith, profitDollMedWoWith, profitPercMedWoWith, profitDollRndWoWith, profitPercRndWoWith, cagrAvgWoWith, cagrMedWoWith, cagrRndWoWith, profitDollAvgWith, profitPercAvgWith, profitDollMedWith, profitPercMedWith, profitDollRndWith, profitPercRndWith, cagrAvgWith, cagrMedWith, cagrRndWith, profitDollAvgWithDrawed, profitPercAvgWithDrawed, profitDollMedWithDrawed, profitPercMedWithDrawed, profitDollRndWithDrawed, profitPercRndWithDrawed, cagrAvgWithDrawed, cagrMedWithDrawed, cagrRndWithDrawed, profitDollAvgTotal, profitPercAvgTotal, profitDollMedTotal, profitPercMedTotal, profitDollRndTotal, profitPercRndTotal, cagrAvgTotal, cagrMedTotal, cagrRndTotal);
    console.log("simulateFunction() END");
}

function writeOutput(totalDeposit, developerInfo, averagePVWoWith, profitDollAvgWoWith, profitPercAvgWoWith, profitDollMedWoWith, profitPercMedWoWith, profitDollRndWoWith, profitPercRndWoWith, cagrAvgWoWith, cagrMedWoWith, cagrRndWoWith, profitDollAvgWith, profitPercAvgWith, profitDollMedWith, profitPercMedWith, profitDollRndWith, profitPercRndWith, cagrAvgWith, cagrMedWith, cagrRndWith, profitDollAvgWithDrawed, profitPercAvgWithDrawed, profitDollMedWithDrawed, profitPercMedWithDrawed, profitDollRndWithDrawed, profitPercRndWithDrawed, cagrAvgWithDrawed, cagrMedWithDrawed, cagrRndWithDrawed, profitDollAvgTotal, profitPercAvgTotal, profitDollMedTotal, profitPercMedTotal, profitDollRndTotal, profitPercRndTotal, cagrAvgTotal, cagrMedTotal, cagrRndTotal) {
    var totalDepositHtmlElement = document.getElementById("idTotalDeposit");
    totalDepositHtmlElement.innerText = totalDeposit.toLocaleString('en-US') + " $";

    var profitDollAvgWoWithHtmlElement = document.getElementById("idProfitDollAvgWoWith");
    profitDollAvgWoWithHtmlElement.innerText = Math.floor(profitDollAvgWoWith).toLocaleString('en-US') + " $";

    var profitDollMedWoWithHtmlElement = document.getElementById("idProfitDollMedWoWith");
    profitDollMedWoWithHtmlElement.innerText = Math.floor(profitDollMedWoWith).toLocaleString('en-US') + " $";

    var profitDollRndWoWithHtmlElement = document.getElementById("idProfitDollRndWoWith");
    profitDollRndWoWithHtmlElement.innerText = Math.floor(profitDollRndWoWith).toLocaleString('en-US') + " $";

    var profitPercAvgWoWithHtmlElement = document.getElementById("idProfitPercAvgWoWith");
    profitPercAvgWoWithHtmlElement.innerText = (Math.floor(profitPercAvgWoWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercMedWoWithHtmlElement = document.getElementById("idProfitPercMedWoWith");
    profitPercMedWoWithHtmlElement.innerText = (Math.floor(profitPercMedWoWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercRndWoWithHtmlElement = document.getElementById("idProfitPercRndWoWith");
    profitPercRndWoWithHtmlElement.innerText = (Math.floor(profitPercRndWoWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRAvgWoWithHtmlElement = document.getElementById("idCAGRAvgWoWith");
    profitCAGRAvgWoWithHtmlElement.innerText = (Math.floor(cagrAvgWoWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRMedWoWithHtmlElement = document.getElementById("idCAGRMedWoWith");
    profitCAGRMedWoWithHtmlElement.innerText = (Math.floor(cagrMedWoWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRRndWoWithHtmlElement = document.getElementById("idCAGRRndWoWith");
    profitCAGRRndWoWithHtmlElement.innerText = (Math.floor(cagrRndWoWith * 1000) / 10).toLocaleString('en-US') + " %";

    //var profitSharpeAvgWoWithHtmlElement = document.getElementById("idSharpeAvgWoWith");
    //profitSharpeAvgWoWithHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeMedWoWithHtmlElement = document.getElementById("idSharpeMedWoWith");
    //profitSharpeMedWoWithHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeRndWoWithHtmlElement = document.getElementById("idSharpeRndWoWith");
    //profitSharpeRndWoWithHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;


    var profitDollAvgWithDrawedHtmlElement = document.getElementById("idProfitDollAvgWithDrawed");
    profitDollAvgWithDrawedHtmlElement.innerText = Math.floor(profitDollAvgWithDrawed).toLocaleString('en-US') + " $";

    var profitDollMedWithDrawedHtmlElement = document.getElementById("idProfitDollMedWithDrawed");
    profitDollMedWithDrawedHtmlElement.innerText = Math.floor(profitDollMedWithDrawed).toLocaleString('en-US') + " $";

    var profitDollRndWithDrawedHtmlElement = document.getElementById("idProfitDollRndWithDrawed");
    profitDollRndWithDrawedHtmlElement.innerText = Math.floor(profitDollRndWithDrawed).toLocaleString('en-US') + " $";

    var profitPercAvgWithDrawedHtmlElement = document.getElementById("idProfitPercAvgWithDrawed");
    profitPercAvgWithDrawedHtmlElement.innerText = (Math.floor(profitPercAvgWithDrawed * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercMedWithDrawedHtmlElement = document.getElementById("idProfitPercMedWithDrawed");
    profitPercMedWithDrawedHtmlElement.innerText = (Math.floor(profitPercMedWithDrawed * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercRndWithDrawedHtmlElement = document.getElementById("idProfitPercRndWithDrawed");
    profitPercRndWithDrawedHtmlElement.innerText = (Math.floor(profitPercRndWithDrawed * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRAvgWithDrawedHtmlElement = document.getElementById("idCAGRAvgWithDrawed");
    profitCAGRAvgWithDrawedHtmlElement.innerText = (Math.floor(cagrAvgWithDrawed * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRMedWithDrawedHtmlElement = document.getElementById("idCAGRMedWithDrawed");
    profitCAGRMedWithDrawedHtmlElement.innerText = (Math.floor(cagrMedWithDrawed * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRRndWithDrawedHtmlElement = document.getElementById("idCAGRRndWithDrawed");
    profitCAGRRndWithDrawedHtmlElement.innerText = (Math.floor(cagrRndWithDrawed * 1000) / 10).toLocaleString('en-US') + " %";

    //var profitSharpeAvgWithDrawedHtmlElement = document.getElementById("idSharpeAvgWithDrawed");
    //profitSharpeAvgWithDrawedHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeMedWithDrawedHtmlElement = document.getElementById("idSharpeMedWithDrawed");
    //profitSharpeMedWithDrawedHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeRndWithDrawedHtmlElement = document.getElementById("idSharpeRndWithDrawed");
    //profitSharpeRndWithDrawedHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    var profitDollAvgStillInvHtmlElement = document.getElementById("idProfitDollAvgStillInv");
    profitDollAvgStillInvHtmlElement.innerText = Math.floor(profitDollAvgWith).toLocaleString('en-US') + " $";

    var profitDollMedStillInvHtmlElement = document.getElementById("idProfitDollMedStillInv");
    profitDollMedStillInvHtmlElement.innerText = Math.floor(profitDollMedWith).toLocaleString('en-US') + " $";

    var profitDollRndStillInvHtmlElement = document.getElementById("idProfitDollRndStillInv");
    profitDollRndStillInvHtmlElement.innerText = Math.floor(profitDollRndWith).toLocaleString('en-US') + " $";

    var profitPercAvgStillInvHtmlElement = document.getElementById("idProfitPercAvgStillInv");
    profitPercAvgStillInvHtmlElement.innerText = (Math.floor(profitPercAvgWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercMedStillInvHtmlElement = document.getElementById("idProfitPercMedStillInv");
    profitPercMedStillInvHtmlElement.innerText = (Math.floor(profitPercMedWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercRndStillInvHtmlElement = document.getElementById("idProfitPercRndStillInv");
    profitPercRndStillInvHtmlElement.innerText = (Math.floor(profitPercRndWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRAvgStillInvHtmlElement = document.getElementById("idCAGRAvgStillInv");
    profitCAGRAvgStillInvHtmlElement.innerText = (Math.floor(cagrAvgWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRMedStillInvHtmlElement = document.getElementById("idCAGRMedStillInv");
    profitCAGRMedStillInvHtmlElement.innerText = (Math.floor(cagrMedWith * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRRndStillInvHtmlElement = document.getElementById("idCAGRRndStillInv");
    profitCAGRRndStillInvHtmlElement.innerText = (Math.floor(cagrRndWith * 1000) / 10).toLocaleString('en-US') + " %";

    //var profitSharpeAvgStillInvHtmlElement = document.getElementById("idSharpeAvgStillInv");
    //profitSharpeAvgStillInvHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeMedStillInvHtmlElement = document.getElementById("idSharpeMedStillInv");
    //profitSharpeMedStillInvHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeRndStillInvHtmlElement = document.getElementById("idSharpeRndStillInv");
    //profitSharpeRndStillInvHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    var profitDollAvgTotalHtmlElement = document.getElementById("idProfitDollAvgTotal");
    profitDollAvgTotalHtmlElement.innerText = Math.floor(profitDollAvgTotal).toLocaleString('en-US') + " $";

    var profitDollMedTotalHtmlElement = document.getElementById("idProfitDollMedTotal");
    profitDollMedTotalHtmlElement.innerText = Math.floor(profitDollMedTotal).toLocaleString('en-US') + " $";

    var profitDollRndTotalHtmlElement = document.getElementById("idProfitDollRndTotal");
    profitDollRndTotalHtmlElement.innerText = Math.floor(profitDollRndTotal).toLocaleString('en-US') + " $";

    var profitPercAvgTotalHtmlElement = document.getElementById("idProfitPercAvgTotal");
    profitPercAvgTotalHtmlElement.innerText = (Math.floor(profitPercAvgTotal * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercMedTotalHtmlElement = document.getElementById("idProfitPercMedTotal");
    profitPercMedTotalHtmlElement.innerText = (Math.floor(profitPercMedTotal * 1000) / 10).toLocaleString('en-US') + " %";

    var profitPercRndTotalHtmlElement = document.getElementById("idProfitPercRndTotal");
    profitPercRndTotalHtmlElement.innerText = (Math.floor(profitPercRndTotal * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRAvgTotalHtmlElement = document.getElementById("idCAGRAvgTotal");
    profitCAGRAvgTotalHtmlElement.innerText = (Math.floor(cagrAvgTotal * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRMedTotalHtmlElement = document.getElementById("idCAGRMedTotal");
    profitCAGRMedTotalHtmlElement.innerText = (Math.floor(cagrMedTotal * 1000) / 10).toLocaleString('en-US') + " %";

    var profitCAGRRndTotalHtmlElement = document.getElementById("idCAGRRndTotal");
    profitCAGRRndTotalHtmlElement.innerText = (Math.floor(cagrRndTotal * 1000) / 10).toLocaleString('en-US') + " %";

    //var profitSharpeAvgTotalHtmlElement = document.getElementById("idSharpeAvgTotal");
    //profitSharpeAvgTotalHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeMedTotalHtmlElement = document.getElementById("idSharpeMedTotal");
    //profitSharpeMedTotalHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;

    //var profitSharpeRndTotalHtmlElement = document.getElementById("idSharpeRndTotal");
    //profitSharpeRndTotalHtmlElement.innerText = Math.floor(averagePVWoWith * 100) / 100;


    var developerInfoHtmlElement = document.getElementById("idDeveloperInfo");
    developerInfoHtmlElement.innerText = developerInfo;
}

console.log("Body is running...");

console.log('SqCore: Script END');