import { Component, EventEmitter, Input, OnInit, Output, ViewChild } from '@angular/core';
import { SharedService } from '@shared/services/shared.service';
import { DateTime } from 'luxon';
import * as moment from 'moment';
import { Canvas } from '../canvas-view/canvas-view.component';

interface records {
  percentage: number;
  color: string;
  checkIn: string;
  checkOut: string;
  totalHours: number;
  paidBreak: string;
  statusname: string;
}
interface canvasRecords {
  checkIn: string;
  checkOut: string;
  totalHours: string;
  paidBreak: string;
  date: any;
  allcheckIn: any;
  allcheckOut: any;
  permissionHours: any;
  status: string;
}

interface precords {
  percentage: number;
  classname: string;
  checkIn: string;
  checkOut: string;
  statusname:string;
}
@Component({
  selector: 'app-progress-display',
  templateUrl: `./progress-bar.html`,
  styleUrls: [`./progress-bar.css`],
})

export class ProgressBar implements OnInit {
  @Input() status!: string;
  @Input() pDate!: DateTime;
  @Input() pCheckin!: DateTime | null;
  @Input() pCheckout!: DateTime | null;
  @Input() tHours: number = 0;
  @Input() allCheckIn: any[];
  @Input() allCheckOut: any[];
  @Input() permissionHours: any[];
  @Output() sendData = new EventEmitter<number>();
  progressPercentage: number = 0;
  progressClass: string = "progress-bar";
  filteredRec = [];
  filteredRecordPresent = [];
  percentages: records[] = [];
  presentPercentanges: precords[] = [];
  shiftStart = 9;
  shiftEnd = 18;
  currentTime = new Date();
  checkinDate: any;
  checkoutDate: any;
  offCanvasTotal: any;
  paidBreak: any;
  selectedDate!: DateTime;
  permission: any;
  newRec: canvasRecords;
  record: any;
  isfullday: boolean = false;
  isHovered: boolean = false;


  @ViewChild('canvasModal', { static: true }) canvasModal: Canvas;


  constructor(public sharedService: SharedService) { }

  ngOnInit() {
    this.displayCheckInCheckOutTimes();
    if (typeof this.pDate === 'string') {
      this.selectedDate = DateTime.fromISO(this.pDate);
    } else if (this.pDate instanceof DateTime) {
      this.selectedDate = this.pDate;
    } else {
      this.selectedDate = DateTime.local();
    }
    if (this.status == "Weekend") {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-weekend", checkIn: '', checkOut: '',statusname:'' });
    }
    else if (this.status == "Absent") {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-absent", checkIn: '', checkOut: '',statusname:''  });
    }
    else if (this.status == "Present") {
      this.filteredRec = [...this.allCheckIn, ...this.allCheckOut].sort((a, b) => a - b);
      this.calculatePercentages();
    }
    else if (this.status == "Present (WFH)") {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-Leave", checkIn: '', checkOut: '',statusname:''  });
    }
    else if (this.status.includes("$")) {
      this.filteredRec = [...this.allCheckIn, ...this.allCheckOut].sort((a, b) => a - b);
      this.calculateHalfDayPercentages(this.status);
    }
    else if (this.status.includes("#")) {
      this.isfullday = true;
      this.calculateHalfDayWfh(this.status);
    }
    else if (this.status.toLocaleLowerCase().includes("holiday")) {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-holiday", checkIn: '', checkOut: '',statusname:''  });
    }
    else if (this.status.toLocaleLowerCase().includes("&")) {
      this.isfullday = true;
      this.filteredRec = [...this.allCheckIn, ...this.allCheckOut].sort((a, b) => a - b);
      this.calculateHalfDayIsLeave(this.status);
    }
    else if (this.status.toLocaleLowerCase().includes("and")) {
      this.isfullday = true;
      this.filteredRec = [...this.allCheckIn, ...this.allCheckOut].sort((a, b) => a - b);
      this.calculateHalfDayLeave(this.status);
    }
    else if (this.status.toLocaleLowerCase().includes("@")) {
      this.isfullday = true;
      this.calculatePaidUnpaidLeave(this.status);
    }
    else if (this.status.toLocaleLowerCase().includes("/")) {
      this.isfullday = true;
      this.calculatePermission(this.status);
    }
    else if (this.status == "") {
      if (this.pCheckin != null) {
        this.sendDataToParent();
      }
      if (this.pCheckin == undefined) {
        this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-Empty", checkIn: '', checkOut: '',statusname:''  });
      } else {
        this.filteredRecordPresent = [...this.allCheckIn, ...this.allCheckOut].sort((a, b) => a - b);
        this.calculatePercentagesDynamic();
      }
    }

    else if (this.status.toLocaleLowerCase().includes("casual leave")) {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-Casual", checkIn: '', checkOut: '',statusname:'CL' });
    }
    else if (this.status.toLocaleLowerCase().includes("sick leave")) {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-Sick", checkIn: '', checkOut: '',statusname:'SL' });
    }
    else if (this.status.toLocaleLowerCase().includes("optional leave")) {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-Optional", checkIn: '', checkOut: '',statusname:'OPH' });
    }
    else if (this.status.toLocaleLowerCase().includes("lop")){
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-absent", checkIn: '', checkOut: '',statusname:''  });
    }
    else if (this.status.toLocaleLowerCase().includes("firsthalf") || this.status.toLocaleLowerCase().includes("secondhalf")) {
      this.presentPercentanges.push({ percentage: 100, classname: "progress-bar-Casual", checkIn: '', checkOut: '',statusname:'' });
    }
  }

  displayCheckInCheckOutTimes() {
    const formattedCheckInCheckOutTimes = [];
    let previousCheckInTime = null;
    let previousCheckOutTime = null;

    for (let i = 0; i < this.allCheckIn.length; i++) {
      const checkInTime = this.allCheckIn[i] ? this.formatTime(this.allCheckIn[i]) : 'No check-in';
      const checkOutTime = this.allCheckOut[i] ? this.formatTime(this.allCheckOut[i]) : 'No check-out';

      if (checkInTime !== previousCheckInTime || checkOutTime !== previousCheckOutTime) {
        formattedCheckInCheckOutTimes.push({
          checkIn: checkInTime,
          checkOut: checkOutTime
        });
        previousCheckInTime = checkInTime;
        previousCheckOutTime = checkOutTime;
      }
    }
    return formattedCheckInCheckOutTimes;
  }

  formatTime(time: DateTime): string {
    if (!(time instanceof DateTime)) {
      return 'Invalid time';
    }
    const formattedTime = time.toFormat('hh:mm a');
    return formattedTime;
  }

  onClick() {
    if (typeof this.selectedDate === 'string') {
      this.selectedDate = DateTime.fromISO(this.selectedDate);
    }
    const formattedDate = this.formatDate(this.selectedDate.toJSDate());
    this.newRec = {
      checkIn: this.pCheckin ? this.formatTimeTo12Hour(this.pCheckin.hour, this.pCheckin.minute) : '--',
      checkOut: this.pCheckout ? this.formatTimeTo12Hour(this.pCheckout.hour, this.pCheckout.minute) : '--',
      totalHours: this.pCheckin && this.pCheckout ? this.convertMinutesToHours(this.tHours || 0) : '--',
      paidBreak: '01:00 Hour(s)',
      date: formattedDate,
      allcheckIn: this.allCheckIn,
      allcheckOut: this.allCheckOut,
      permissionHours: this.permissionHours,
      status: this.status
    };
    this.sharedService.setValue(this.newRec);
  }

  sendDataToParent() {
    this.sendData.emit(this.tHours);
  }

  convertMinutesToHours(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    const formattedHours = hours < 10 ? `0${hours}` : hours.toString();
    const formattedMinutes = mins < 10 ? `0${mins}` : mins.toString();
    return `${formattedHours}:${formattedMinutes} Hour(s)`;
  }

  formatDate(date: Date): string {
    return moment(date).format("ddd, DD  MMM , YYYY");
  }

  formatTimeTo12Hour(hour: number, minute: number): string {
    const period = hour >= 12 ? 'PM' : 'AM';
    const formattedHour = hour % 12 || 12;
    const formattedMinute = minute < 10 ? `0${minute}` : minute;
    return `${formattedHour}:${formattedMinute} ${period}`;
  }

  calculatePercentageOfDay() {
    let shiftStartTime = new Date(new Date().setHours(9, 0, 0, 0));
    let shiftEndTime = new Date(new Date().setHours(18, 0, 0, 0));
    let checkinStarted = (new Date(new Date(this.allCheckIn[0]).setHours(this.allCheckIn[0].hour, this.allCheckIn[0].minute, this.allCheckIn[0].second, this.allCheckIn[0].millisecond)).getTime());
    let delayTime = this.allCheckIn[0] != undefined && (checkinStarted >
      shiftStartTime.getTime()) ?
      (checkinStarted - shiftStartTime.getTime()) : null;
    const durationMin = delayTime / (1000 * 60 * 60);
    const totalShiftDuration = shiftEndTime.getTime() - shiftStartTime.getTime();
    const totalShiftDurationMin = totalShiftDuration / (1000 * 60 * 60);
    const percentage = (durationMin / totalShiftDurationMin) * 100;
    const localPercent = Math.round(percentage);
    if (delayTime != null) {
      this.presentPercentanges.push({ percentage: localPercent, classname: "#FFFFF", checkIn: '', checkOut: '',statusname:'' });
    }
    if (localPercent < 100) {
      let endTime = new Date().getTime();
      const totalShiftDuration = endTime - checkinStarted;
      const totalShiftDurationMin = totalShiftDuration / (1000 * 60 * 60);
      const percentage = (durationMin / totalShiftDurationMin) * 100;
      const localPercent2 = Math.round(percentage);
      this.presentPercentanges.push({ percentage: localPercent2, classname: "progress-bar progress-bar-striped progress-bar-animated progress-bar-Missed", checkIn: '', checkOut: '',statusname:'' });
      let addedPercentage = localPercent + localPercent2;
      if (addedPercentage < 100) {
        this.presentPercentanges.push({ percentage: (100 - addedPercentage), classname: "#FFFFF", checkIn: '', checkOut: '',statusname:'' });
      }
    }

  }

  calculatePermission(statusValues: string) {
    const statusArray = statusValues.split('/').map(s => s.trim());
    const leaveTypes = [
      "Casual Leave",
      "Sick Leave",
      "Optional Leave",
      "Leave Without Pay (LOP)",
    ];

    for (const [index, value] of statusArray.entries()) {
      let statusType = '';
      let leaveType = '';
      let halfDayType = index === 0 ? 'FirstHalf' : 'SecondHalf';
      if (value.includes('Present')) {
        statusType = 'Present';
      } else if (value.includes('Leave')) {
        statusType = 'Leave';
          for (const type of leaveTypes) {
          if (value.includes(type)) {
            leaveType = type;
            break;
          }
        }
       if (!leaveType) {
          leaveType = 'Leave';
        }
      }
      if (halfDayType === 'FirstHalf' && statusType === 'Present') {
         this.percentages.push({
          percentage: 50,
          color: "rgba(191, 228, 191, 0.75)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 Present (FirstHalf)'
        });
      } else if (halfDayType === 'FirstHalf' && statusType === 'Leave') {
        this.percentages.push({
          percentage: 50,
          color: "rgba(152, 154, 187, 0.35)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveType} (FirstHalf)`
        });
      } else if (halfDayType === 'SecondHalf' && statusType === 'Present') {
        this.percentages.push({
          percentage: 50,
          color: "rgba(191, 228, 191, 0.75)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 Present (SecondHalf)'
        });
      } else if (halfDayType === 'SecondHalf' && statusType === 'Leave') {
        this.percentages.push({
          percentage: 50,
          color: "rgba(152, 154, 187, 0.35)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveType} (SecondHalf)`
        });
      }
    }
  }

  calculatePaidUnpaidLeave(statusValues: string): void {
    const statusArray = statusValues.split('@').map(s => s.trim());
    for (const value of statusArray) {
      let halfdayStatus = '';
      let percentage = 50;
      if (value.includes('FirstHalf')) {
        const firstHalfStatus = value.split(' ').splice(0, value.split(' ').length - 1).join(' ');
        halfdayStatus = firstHalfStatus;
        if (value.toLowerCase().includes('lop')) {
          this.percentages.push({
            percentage: percentage,
            color: "rgba(252, 176, 176, 0.35)",
            checkIn: '',
            checkOut: '',
            totalHours: this.tHours,
            paidBreak: '01:00Hrs',
            statusname: `0.5 ${halfdayStatus || 'LOP'} (FirstHalf)`
          });
        } else {
          this.percentages.push({
            percentage: percentage,
            color: "rgba(152, 154, 187, 0.35)",
            checkIn: '',
            checkOut: '',
            totalHours: this.tHours,
            paidBreak: '01:00Hrs',
            statusname: `0.5 ${halfdayStatus || 'Leave'} (FirstHalf)`
          });
        }
      }

      if (value.includes('SecondHalf')) {
        const secondHalfStatus = value.split(' ').splice(0, value.split(' ').length - 1).join(' ');
        halfdayStatus = secondHalfStatus;
        if (value.toLowerCase().includes('lop')) {
          this.percentages.push({
            percentage: percentage,
            color: "rgba(252, 176, 176, 0.35)",
            checkIn: '',
            checkOut: '',
            totalHours: this.tHours,
            paidBreak: '01:00Hrs',
            statusname: `0.5 ${halfdayStatus || 'LOP'} (SecondHalf)`
          });
        } else {
          this.percentages.push({
            percentage: percentage,
            color: "rgba(152, 154, 187, 0.35)",
            checkIn: '',
            checkOut: '',
            totalHours: this.tHours,
            paidBreak: '01:00Hrs',
            statusname: `0.5 ${halfdayStatus || 'Leave'} (SecondHalf)`
          });
        }
      }
    }
  }

  calculateHalfDayPercentages(statusValues): void {
    const statusArray = statusValues.split('$').map(s => s.trim());
    for (const value of statusArray) {
      if (value.includes('P')) {
        this.percentages.push({
          percentage: 50, color: "rgba(191, 228, 191, 0.75)", checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
          checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute, totalHours: this.tHours, paidBreak: '01:00Hrs', statusname: '0.5 Present'
        });
      }
      else if (value.includes('A')) {
        this.percentages.push({
          percentage: 50, color: "rgba(252, 176, 176, 0.35)", checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
          checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute, totalHours: this.tHours, paidBreak: '01:00Hrs', statusname: '0.5 Absent'
        });
      }
    }
  }

  calculateHalfDayWfh(statusValues: string): void {
    const statusArray = statusValues.split('#').map(s => s.trim());
    for (const value of statusArray) {
      let leaveHalfDay = '';
      if (value.includes('Leave')) {
        const leaveValue = value.split(' ');
        leaveHalfDay = leaveValue.slice(-2).join(' ');
      }

      if (value.includes('Present (WFH)')) {
        this.percentages.push({
          percentage: 50,
          color: "#BFE4BF",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 WFH'
        });
      } else if (value.includes('A')) {
        this.percentages.push({
          percentage: 50,
          color: "rgba(252, 176, 176, 0.35)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 Absent'
        });
      } else if (value.includes('FirstHalf')) {
        this.percentages.push({
          percentage: 50,
          color: "rgba(152, 154, 187, 0.35)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveHalfDay || 'Leave'}`
        });
      } else if (value.includes('SecondHalf')) {
        this.percentages.push({
          percentage: 50,
          color: "rgba(152, 154, 187, 0.35)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveHalfDay || 'Leave'}`
        });
      }
    }
  }


  calculateHalfDayIsLeave(statusValues: string) {
    const statusArray = statusValues.split('&').map(s => s.trim());
    for (const value of statusArray) {
      let leaveName = '';
      if (value.includes('LOP') || value.includes('Leave')) {
        leaveName = value.includes('LOP') ? 'LOP' : value.split(' ')[0];
      }

      if (value.includes('FirstHalf')) {
        this.percentages.push({
          percentage: 50,
          color: leaveName === 'LOP' ? "rgba(252, 176, 176, 0.35)" : "#989ABB59",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveName} (FirstHalf)`
        });
      } else if (value.includes('SecondHalf')) {
        this.percentages.push({
          percentage: 50,
          color: leaveName === 'LOP' ? "rgba(252, 176, 176, 0.35)" : "#989ABB59",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveName} (SecondHalf)`
        });
      } else if (value.includes('Present')) {
        this.percentages.push({
          percentage: 50,
          color: "rgba(191, 228, 191, 0.75)",
          checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
          checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute,
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 Present'
        });
      } else if (value.includes('Absent')) {
        this.percentages.push({
          percentage: 50,
          color: "rgba(252, 176, 176, 0.35)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 Absent'
        });
      }
    }
  }

  calculateHalfDayLeave(statusValues: string) {
    const statusArray = statusValues.split('and').map(s => s.trim());

    for (const value of statusArray) {
      let leaveWFH = '';
      if (value.includes('LOP')) {
        leaveWFH = 'LOP';
      } else if (value.includes('0.5 Present (WFH)')) {
        leaveWFH = '0.5 Present (WFH)';
      }
      if (value.includes('FirstHalf')) {
        this.percentages.push({
          percentage: 50,
          color: leaveWFH === 'LOP' ? "rgba(252, 176, 176, 0.35)" : "#989ABB59",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveWFH} (FirstHalf)`
        });
      } else if (value.includes('SecondHalf')) {
        this.percentages.push({
          percentage: 50,
          color: leaveWFH === 'LOP' ? "rgba(252, 176, 176, 0.35)" : "#989ABB59",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: `0.5 ${leaveWFH} (SecondHalf)`
        });
      } else if (value.includes('0.5 Present (WFH)')) {
        this.percentages.push({
          percentage: 50,
          color: "rgba(191, 228, 191, 0.75)",
          checkIn: '',
          checkOut: '',
          totalHours: this.tHours,
          paidBreak: '01:00Hrs',
          statusname: '0.5 WFH'
        });
      }
    }
  }


  calculatePercentages(): void {
    let shiftStartTime = new Date(this.filteredRec[0].toString());
    shiftStartTime.setHours(9, 0, 0, 0);
    let shiftEndTime = new Date(this.filteredRec[0].toString());
    shiftEndTime.setHours(18, 0, 0, 0);
    const totalShiftDurationMs = shiftEndTime.getTime() - shiftStartTime.getTime();
    const totalShiftDurationMin = totalShiftDurationMs / (1000 * 60 * 60);
    this.percentages = [];
    let tempDate = this.filteredRec[0];
    let delayTime = this.filteredRec[0] != undefined &&
      (new Date(new Date(this.filteredRec[0]).setHours(this.filteredRec[0].hour, this.filteredRec[0].minute, this.filteredRec[0].second, this.filteredRec[0].millisecond)).getTime() >
        new Date(new Date(tempDate).setHours(9, 0, 0, 0)).getTime()) ?
      new Date(new Date(this.filteredRec[0]).setHours(this.filteredRec[0].hour, this.filteredRec[0].minute, this.filteredRec[0].second, this.filteredRec[0].millisecond)).getTime() - shiftStartTime.getTime() : null;
    const durationMin = delayTime / (1000 * 60 * 60);
    let statusname = "Present";
    if (delayTime != null) {
      const percentage = (durationMin / totalShiftDurationMin) * 100;
      const localPercent = Math.round(percentage);
      this.percentages.push({
        percentage: localPercent, color: "#E9ECEF", checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
        checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute, totalHours: this.tHours, paidBreak: '01:00Hrs', statusname: statusname
      });
      statusname = "";
    }
    for (let i = 0; i < this.filteredRec.length; i += 2) {
      const startTime = new Date(new Date(this.filteredRec[i]).setHours(this.filteredRec[i].hour, this.filteredRec[i].minute, this.filteredRec[i].second, this.filteredRec[i].millisecond)).getTime();
      let endTime = 0;
      if (this.filteredRec[i + 1] !== undefined) {
        endTime = new Date(new Date(this.filteredRec[i + 1]).setHours(this.filteredRec[i + 1].hour, this.filteredRec[i + 1].minute, this.filteredRec[i + 1].second, this.filteredRec[i + 1].millisecond)).getTime();
      } else if (new Date(new Date(this.filteredRec[i]).setHours(this.filteredRec[i].hour, this.filteredRec[i].minute, this.filteredRec[i].second, this.filteredRec[i].millisecond)).getTime() === new Date().getDate()) {
        endTime = new Date().getTime();
      } else {
        endTime = new Date(new Date(this.filteredRec[i]).setHours(this.filteredRec[i].hour, this.filteredRec[i].minute, this.filteredRec[i].second, this.filteredRec[i].millisecond)).getTime();
      }
      const durationMs = endTime - startTime;
      const durationMin = durationMs / (1000 * 60 * 60);
      const percentage = (durationMin / totalShiftDurationMin) * 100;
      const localPercent = Math.round(percentage);
      const localColor = (this.filteredRec.length % 2 === 0) ||
        (new Date(new Date(this.filteredRec[i]).setHours(this.filteredRec[i].hour, this.filteredRec[i].minute, this.filteredRec[i].second, this.filteredRec[i].millisecond)).getTime() === new Date().getDate()) ?
        "rgba(191, 228, 191, 0.75)" : "rgba(191, 228, 191, 0.75)";
      this.percentages.push({
        percentage: localPercent, color: localColor, checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
        checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute, totalHours: this.tHours, paidBreak: '01:00Hrs', statusname: statusname
      });
      statusname = "";
      if (this.filteredRec[i + 1] !== undefined && this.filteredRec[i + 2] !== undefined) {
        const bStartTime = new Date(new Date(this.filteredRec[i]).setHours(this.filteredRec[i].hour, this.filteredRec[i].minute, this.filteredRec[i].second, this.filteredRec[i].millisecond)).getTime();
        const bEndTime = new Date(new Date(this.filteredRec[i + 1]).setHours(this.filteredRec[i + 1].hour, this.filteredRec[i + 1].minute, this.filteredRec[i + 1].second, this.filteredRec[i + 1].millisecond)).getTime();
        const bDurationMs = bEndTime - bStartTime;
        const bDurationMin = bDurationMs / (1000 * 60 * 60);
        const bPercentage = Math.round((bDurationMin / totalShiftDurationMin) * 100);
        this.percentages.push({
          percentage: bPercentage, color: "#E9ECEF", checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
          checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute, totalHours: this.tHours, paidBreak: '01:00Hrs', statusname: statusname
        });
      }
    }
  }

  getStatusColor(record: records) {
    if (this.status == "Weekend") {
      return "weekend"
    }
    if (this.status == "Absent") {
      return "Absent"
    }
    if (this.status == "Present") {
      return "Presents"
    }
    if (this.status.toLocaleLowerCase().includes("casual leave")) {
      return "CasualLeave"
    }
    if (this.status == "Sick Leave") {
      return "SickLeave"
    }
    if (this.status == "Optional Leave") {
      return "OptionalLeave"
    }
    if (this.status == "LOP") {
      return "Absent"
    }
    if (this.status == "0.5 Absent") {
      return "Absents"
    }
    if (this.status == "Present (WFH)") {
      return "PresentWfh"
    }
    if (this.status == "0.5 WFH") {
      return "PresentWfh"
    }
    if (this.status.toLocaleLowerCase().includes("holiday")) {
      return "PresentWfh"
    }
    if (this.status.toLocaleLowerCase().includes("firsthalf")) {
      return "CasualLeave"
    }
    if (this.status.toLocaleLowerCase().includes("secondhalf")) {
      return "CasualLeave"
    }
  }


  calculatePercentagesDynamic(): void {
    let shiftStartTime = new Date(this.filteredRecordPresent[0].toString());
    shiftStartTime.setHours(9, 0, 0, 0);
    let shiftEndTime = new Date(this.filteredRecordPresent[0].toString());
    shiftEndTime.setHours(18, 0, 0, 0);

    const totalShiftDurationMs = shiftEndTime.getTime() - shiftStartTime.getTime();
    const totalShiftDurationMin = totalShiftDurationMs / (1000 * 60); // Convert to minutes

    this.presentPercentanges = []; // Initialize percentages array

    // Get the current time
    const currentTime = new Date().getTime();

    // Calculate delay time for late check-in
    let tempDate = this.filteredRecordPresent[0];
    let userCheckInTime = new Date(tempDate).setHours(
      tempDate.hour,
      tempDate.minute,
      tempDate.second
    );

    let delayTime = (userCheckInTime > shiftStartTime.getTime()) ?
      userCheckInTime - shiftStartTime.getTime() :
      null;

    if (delayTime != null) {
      const delayMinutes = delayTime / (1000 * 60); // Delay time in minutes
      const percentage = (delayMinutes / totalShiftDurationMin) * 100;
      const localPercent = Math.round(percentage);

      // Push the late percentage to be displayed
      this.presentPercentanges.push({
        percentage: localPercent,
        classname: "progress-bar-late", // Style for late entries
        checkIn: `${this.pCheckin.hour}:${this.pCheckin.minute}`,
        checkOut: '' // Will update check-out later,
        ,statusname:''
      });
    }

    // Continue calculating progress for the remaining shift hours
    for (let i = 0; i < this.filteredRecordPresent.length; i += 2) {
      const startTime = new Date(new Date(this.filteredRecordPresent[i]).setHours(
        this.filteredRecordPresent[i].hour, this.filteredRecordPresent[i].minute, this.filteredRecordPresent[i].second
      )).getTime();

      let endTime = (this.filteredRecordPresent[i + 1] !== undefined) ?
        new Date(new Date(this.filteredRecordPresent[i + 1]).setHours(
          this.filteredRecordPresent[i + 1].hour, this.filteredRecordPresent[i + 1].minute, this.filteredRecordPresent[i + 1].second
        )).getTime() :
        currentTime; // Use current time if no check-out recorded

      const durationMs = endTime - startTime;
      const durationMin = durationMs / (1000 * 60); // Convert to minutes
      const percentage = (durationMin / totalShiftDurationMin) * 100;
      const localPercent = Math.round(percentage);

      // Determine the classname based on time
      const isCurrentCheckIn = (endTime >= currentTime);
      const progressBarClass = isCurrentCheckIn ?
        "progress-bar progress-bar-striped progress-bar-Missed" :
        "progress-bar progress-bar-Missed"; // For past check-ins

      // Push this period's percentage
      this.presentPercentanges.push({
        percentage: localPercent,
        classname: progressBarClass,
        checkIn: `${this.pCheckin.hour}:${this.pCheckin.minute}`,
        checkOut: this.pCheckout ? `${this.pCheckout.hour}:${this.pCheckout.minute}` : 'In Progress',
        statusname:''
      });

      // Now calculate the gap if there's a break between this check-out and the next check-in
      if (this.filteredRecordPresent[i + 1] !== undefined && this.filteredRecordPresent[i + 2] !== undefined) {
        const nextCheckIn = new Date(new Date(this.filteredRecordPresent[i + 2]).setHours(
          this.filteredRecordPresent[i + 2].hour, this.filteredRecordPresent[i + 2].minute, this.filteredRecordPresent[i + 2].second
        )).getTime();

        const gapDurationMs = nextCheckIn - endTime;
        if (gapDurationMs > 0) {
          const gapDurationMin = gapDurationMs / (1000 * 60); // Convert to minutes
          const gapPercentage = (gapDurationMin / totalShiftDurationMin) * 100;
          const gapPercentRounded = Math.round(gapPercentage);

          // Push the break time as a gap percentage
          this.presentPercentanges.push({
            percentage: gapPercentRounded,
            classname: "progress-bar-break", // Style for break periods
            checkIn: `${new Date(endTime).getHours()}:${new Date(endTime).getMinutes()}`,
            checkOut: `${new Date(nextCheckIn).getHours()}:${new Date(nextCheckIn).getMinutes()}`
            ,statusname:''
          });
        }
      }
    }
  }



  // calculatePercentagesDynamic(): void {
  //     let shiftStartTime = new Date(this.filteredRecordPresent[0].toString());
  //     shiftStartTime.setHours(9, 0, 0, 0);
  //     let shiftEndTime = new Date(this.filteredRecordPresent[0].toString());
  //     shiftEndTime.setHours(18, 0, 0, 0);
  //     const totalShiftDurationMs = shiftEndTime.getTime() - shiftStartTime.getTime();
  //     const totalShiftDurationMin = totalShiftDurationMs / (1000 * 60 * 60);
  //     this.percentages = [];
  //     let tempDate = this.filteredRecordPresent[0];
  //     let delayTime = this.filteredRecordPresent[0] != undefined &&
  //       (new Date(new Date(this.filteredRecordPresent[0]).setHours(this.filteredRecordPresent[0].hour, this.filteredRecordPresent[0].minute, this.filteredRecordPresent[0].second, this.filteredRecordPresent[0].millisecond)).getTime() >
  //         new Date(new Date(tempDate).setHours(9, 0, 0, 0)).getTime()) ?
  //       new Date(new Date(this.filteredRecordPresent[0]).setHours(this.filteredRecordPresent[0].hour, this.filteredRecordPresent[0].minute, this.filteredRecordPresent[0].second, this.filteredRecordPresent[0].millisecond)).getTime() - shiftStartTime.getTime() : null;
  //     const durationMin = delayTime / (1000 * 60 * 60);
  //     let statusname = "Present";
  //     if (delayTime != null) {
  //       const percentage = (durationMin / totalShiftDurationMin) * 100;
  //       const localPercent = Math.round(percentage);
  //       this.presentPercentanges.push({ percentage: localPercent, classname: "#E9ECEF", checkIn: '', checkOut: '' });
  //       statusname = "";
  //     }
  //     for (let i = 0; i < this.filteredRecordPresent.length; i += 2) {
  //       const startTime = new Date(new Date(this.filteredRecordPresent[i]).setHours(this.filteredRecordPresent[i].hour, this.filteredRecordPresent[i].minute, this.filteredRecordPresent[i].second, this.filteredRecordPresent[i].millisecond)).getTime();
  //       let endTime = 0;
  //       if (this.filteredRecordPresent[i + 1] !== undefined) {
  //         endTime = new Date(new Date(this.filteredRecordPresent[i + 1]).setHours(this.filteredRecordPresent[i + 1].hour, this.filteredRecordPresent[i + 1].minute, this.filteredRecordPresent[i + 1].second, this.filteredRecordPresent[i + 1].millisecond)).getTime();
  //       } else if (new Date(new Date(this.filteredRecordPresent[i]).setHours(this.filteredRecordPresent[i].hour, this.filteredRecordPresent[i].minute, this.filteredRecordPresent[i].second, this.filteredRecordPresent[i].millisecond)).getTime() === new Date().getDate()) {
  //         endTime = new Date().getTime();
  //       } else {
  //         endTime = new Date(new Date(this.filteredRecordPresent[i]).setHours(this.filteredRecordPresent[i].hour, this.filteredRecordPresent[i].minute, this.filteredRecordPresent[i].second, this.filteredRecordPresent[i].millisecond)).getTime();
  //       }
  //       const durationMs = endTime - startTime;
  //       const durationMin = durationMs / (1000 * 60 * 60);
  //       const percentage = (durationMin / totalShiftDurationMin) * 100;
  //       const localPercent = Math.round(percentage);
  //       const localColor = (this.filteredRecordPresent.length % 2 === 0) ||
  //         (new Date(new Date(this.filteredRecordPresent[i]).setHours(this.filteredRecordPresent[i].hour, this.filteredRecordPresent[i].minute, this.filteredRecordPresent[i].second, this.filteredRecordPresent[i].millisecond)).getTime() === new Date().getDate()) ?
  //         "progress-bar progress-bar-striped progress-bar-animated progress-bar-Missed" : "progress-bar progress-bar-striped progress-bar-animated progress-bar-Missed";
  //       this.presentPercentanges.push({
  //         percentage: localPercent, classname: localColor, checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
  //         checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute
  //       });
  //       statusname = "";
  //       if (this.filteredRecordPresent[i + 1] !== undefined && this.filteredRecordPresent[i + 2] !== undefined) {
  //         const bStartTime = new Date(new Date(this.filteredRecordPresent[i]).setHours(this.filteredRecordPresent[i].hour, this.filteredRecordPresent[i].minute, this.filteredRecordPresent[i].second, this.filteredRecordPresent[i].millisecond)).getTime();
  //         const bEndTime = new Date(new Date(this.filteredRecordPresent[i + 1]).setHours(this.filteredRecordPresent[i + 1].hour, this.filteredRecordPresent[i + 1].minute, this.filteredRecordPresent[i + 1].second, this.filteredRecordPresent[i + 1].millisecond)).getTime();
  //         const bDurationMs = bEndTime - bStartTime;
  //         const bDurationMin = bDurationMs / (1000 * 60 * 60);
  //         const bPercentage = Math.round((bDurationMin / totalShiftDurationMin) * 100);
  //         this.presentPercentanges.push({
  //           percentage: bPercentage, classname: "#009ef7", checkIn: this.pCheckin.hour + ":" + this.pCheckin.minute,
  //           checkOut: this.pCheckout.hour + ":" + this.pCheckout.minute
  //         });
  //       }
  //     }
  //   }

}
