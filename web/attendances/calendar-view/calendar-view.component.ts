import { Component, Input, Injector } from '@angular/core';
import { AttendanceDetails, AttendanceDetailsForFooter, AttendancesServiceProxy } from '@shared/service-proxies/service-proxies';
import { AbpSessionService } from 'abp-ng2-module';
import { DatePipe } from '@angular/common';
import { DateTimeService } from '@app/shared/common/timing/date-time.service';
import { AppSessionService } from '@shared/common/session/app-session.service';
import { PermissionCheckerService } from 'abp-ng2-module';
import { AppConsts } from '@shared/AppConsts';
import { AppComponentBase } from '@shared/common/app-component-base';

@Component({
  selector: 'app-calendar-view',
  templateUrl: './calendar-view.component.html',
  styleUrls: ['./calendar-view.component.css']
})
export class CalendarViewComponent extends AppComponentBase {

  profilePicture = AppConsts.appBaseUrl + '/assets/common/images/default-profile-picture.png';
  records: AttendanceDetails[];
  modelDate: Date = new Date();
  attendance: AttendanceDetails[] = [];
  reportFiltersInit: any = {
    "teamIds": []
  }
  selectedUserId: string;
  selectedTeamId: string;
  empName: string;
  empFname: string;
  empSname:string;
  userid: string;
  emailAddress: string;
  permission: PermissionCheckerService;

  constructor(
    injector: Injector,
    private _attendancesServiceProxy: AttendancesServiceProxy,
    private _abpSessionService: AbpSessionService,
    private datePipe: DatePipe,
    private _dateTimeService: DateTimeService,
    private appsession: AppSessionService

  ) { super(injector); }

  @Input("item3") item;

  ngOnInit(): void {
    // this.currentLoginUserInformations();
    this.getAllDropdownList();
    this.selectedTeamId = this._abpSessionService.userId.toString();
    if (this.item) {
      this.getAttendances(this.selectedUserId); // Fetch attendances on component initialization
    }
  }

  prevMonth() {
    let prevMonth = new Date(this.modelDate);
    prevMonth.setMonth(prevMonth.getMonth() - 1);
    if (this.modelDate.getDate() !== prevMonth.getDate()) {
      prevMonth.setDate(0);
    }
    this.modelDate = new Date(prevMonth);
    this.getAttendances(this.selectedUserId);
  }

  nextMonth() {
    let nextMonth = new Date(this.modelDate);
    nextMonth.setMonth(nextMonth.getMonth() + 1);
    if (this.modelDate.getDate() !== nextMonth.getDate()) {
      nextMonth.setDate(0);
    }
    this.modelDate = new Date(nextMonth);
    this.getAttendances(this.selectedUserId);
  }

  currentdate() {
    this.modelDate = new Date();
    this.getAttendances(this.selectedUserId);
  }

  onDateChange(newDate: Date) {
    this.modelDate = new Date(newDate);
    this.getAttendances(this.selectedUserId);
  }

  getFormattedDate() {
    return this.datePipe.transform(this.modelDate, 'MMMM yyyy');
  }

  // currentLoginUserInformations(): void {
  //   this.empName = this.appsession.user.userName;
  //   this.empFname = this.appsession.user.name;
  //   this.empSname = this.appsession.user.surname;
  //   this.emailAddress = this.appsession.user.emailAddress;
  //    this.userid = this.appsession.user.id.toString();
  // }

  getAllDropdownList() {
    this._attendancesServiceProxy.customGetTeamDetails()
      .subscribe((res) => {
        this.sortDropdown(res.teamIds); 
      });     
  }

  sortDropdown(records){
    let removeRecord;
    this.reportFiltersInit.teamIds = records;
     for(let d of records){
      if(d.userId == this._abpSessionService.userId){
        removeRecord = d;
      }
     }
     if (removeRecord) {
     this.reportFiltersInit.teamIds = records.filter(x => x.userId != removeRecord.userId);  
      this.reportFiltersInit.teamIds.unshift(removeRecord);
     }
  }

  isPermissionsGranted() {
    return this.permission.isGranted("Pages.TimesheetApprovals") ||
      this.permission.isGranted("Pages.LeaveApprovals") ||
      this.permission.isGranted('Pages.PermissionApprovals')
  }


  getAttendances(userId?: any) {
    if (userId != null || userId != undefined) {
      this.selectedUserId = userId;
    } else {
      this.selectedUserId = this._abpSessionService.userId.toString();
    }
    
    this._attendancesServiceProxy.getAllCustom(
      undefined,
      undefined,
      this._dateTimeService.getStartOfDayForDateUTC(this.modelDate),
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      this.selectedUserId,
      undefined,
      undefined,
      40
    ).subscribe(result => {
      const year = this.modelDate.getFullYear();
      const month = this.modelDate.getMonth();
      const firstDayOfMonth = new Date(year, month, 1).getDay(); // Get day of the week for the 1st day (0 for Sunday, 1 for Monday, ...)
      this.attendance = [];
      // Add empty cards based on the first day of the month
      for (let i = 0; i < firstDayOfMonth; i++) {
        let att = new AttendanceDetails();
        att.firstIn = null;
        this.attendance.push(att);
      }
      this.attendance.push(...result.attendanceDetails);
    });
  }

  isPresentDay(date: any) {
    const day = new Date(date);
    const currentDate = new Date(); // Creates a new Date object representing the current date and time

    if (day.getFullYear() === currentDate.getFullYear() &&
      day.getMonth() === currentDate.getMonth() &&
      day.getDate() === currentDate.getDate()) {
      return true;
    } else {
      return false;
    }
  }
  isWeekend(date: any) {
    var day = new Date(date);
    const dayOfWeek = day.getDay();
    if (dayOfWeek === 0 /* Sunday */ || dayOfWeek === 6 /* Saturday */) {
      return true;
    } else {
      return false;
    }
  }

  getDayClass(day: any): string {
    if (day.date === 0) {
      return 'empty-day';
    } else if (day.day === 'Sat' || day.day === 'Sun') {
      return 'weekend';
    } else {
      return '';
    }
  }

  weekdays: string[] = ['Sun', 'Mon', 'Tue', 'Wed', 'Thur', 'Fri', 'Sat'];


   // This function strips the time and creates a new date using only the date parts.
stripTime(utcDate: string): string {
  const date = new Date(utcDate);
  // Return the date as "yyyy-mm-dd" to ignore time
  const year = date.getUTCFullYear();
  const month = ('0' + (date.getUTCMonth() + 1)).slice(-2); // Months are zero-indexed
  const day = ('0' + date.getUTCDate()).slice(-2);

  return `${year}-${month}-${day}`; // Format: yyyy-mm-dd
}
}