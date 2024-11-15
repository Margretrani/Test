import { Component, Injector, Input, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { EntityTypeHistoryModalComponent } from '@app/shared/common/entityHistory/entity-type-history-modal.component';
import { DateTimeService } from '@app/shared/common/timing/date-time.service';
import { AppConsts } from '@shared/AppConsts';
import { AppComponentBase } from '@shared/common/app-component-base';
import { AppSessionService } from '@shared/common/session/app-session.service';
import { AttendancesServiceProxy, FooterDetails } from '@shared/service-proxies/service-proxies';
import { ExportService } from '@shared/services/export-service';
import { SharedService } from '@shared/services/shared.service';
import { AbpSessionService } from 'abp-ng2-module';
import { DateTime } from 'luxon';
import * as moment from 'moment';
import { BsDatepickerConfig } from 'ngx-bootstrap/datepicker';
import { LazyLoadEvent } from 'primeng/api/lazyloadevent';
import { Dropdown } from 'primeng/dropdown';
import { Paginator } from 'primeng/paginator';
import { Table } from 'primeng/table';
import { CreateOrEditAttendanceModalComponent } from '../create-or-edit-attendance-modal.component';
import { ViewAttendanceModalComponent } from '../view-attendance-modal.component';

@Component({
  selector: 'app-list-view',
  templateUrl: './list-view.component.html',
  styleUrls: ['./list-view.component.css']
})
export class ListViewComponent extends AppComponentBase {
  profilePicture = AppConsts.appBaseUrl + '/assets/common/images/default-profile-picture.png';
  @Input("item") itemlistview;
  @Input("item2") item;
  @Input() profileImageCssClass = '';
  @ViewChild('entityTypeHistoryModal', { static: true }) entityTypeHistoryModal: EntityTypeHistoryModalComponent;
  @ViewChild('createOrEditAttendanceModal', { static: true }) createOrEditAttendanceModal: CreateOrEditAttendanceModalComponent;
  @ViewChild('viewAttendanceModal', { static: true }) viewAttendanceModal: ViewAttendanceModalComponent;
  @ViewChild('dataTable', { static: true }) dataTable: Table;
  @ViewChild('paginator', { static: true }) paginator: Paginator;
  @ViewChild('dropdown') dropdown: Dropdown;
  @ViewChild('searchButton') searchButton: any;
  totalHoursFromProgressBar: number = 0;
  selectedTeamId: string;
  selectedUserId: string;
  weekEnd: moment.Moment;
  weekStart: moment.Moment;
  selectedDate: any;
  selectedView: string = 'week';
  selectedMonth: string = '';
  selectedMember: number = null;
  modelDate: any;
  bsDatepickerConfig: Partial<BsDatepickerConfig> = {};
  userId: string;
  calendar: FormGroup;
  reportFiltersInit: any = {
    "teamIds": []
  }
  empName: string;
  emailAddress: string;
  shiftStart = 9;
  shiftEnd = 18;
  timeList: string[] = [];
  footerDetails: FooterDetails;
  isEarly: boolean;
  sample: string = "00:00";
  constructor(
    injector: Injector,
    private _attendancesServiceProxy: AttendancesServiceProxy,
    private _dateTimeService: DateTimeService,
    private _abpSession: AbpSessionService,
    private _sharedService: SharedService,
    private appsession: AppSessionService,
    private exportService: ExportService,
    private _abpSessionService: AbpSessionService,
  ) {
    super(injector);
    this.calendar = new FormGroup({
      calType: new FormControl('0'),
    });
  }

  ngOnInit(): void {

    for (let tHour = this.shiftStart; tHour <= this.shiftEnd; tHour++) {
        const ampm = tHour >= 12 ? 'PM' : 'AM';

        // Special handling for midnight (0:00) and noon (12:00)
        const displayHour = tHour === 0 ? 12 : tHour % 12 === 0 ? 12 : tHour % 12;

        // Show "00:00 AM" for midnight
        const formattedTime = tHour === 0 ? `00:00 AM` : `${displayHour}:00 ${ampm}`;

        this.timeList.push(formattedTime);
      }

    this.currentLoginUserInformations();
    this.getAllDropdownList();
    this.currentDate();
    this.selectedTeamId = this._abpSession.userId.toString();
       if (this.itemlistview) {
      this._sharedService.triggerCheckInOutAction.subscribe((data) => {
        if (data != null) {
          this.getAttendances();
        }
      });
    }
  }
 // Method to convert 24-hour format to 12-hour format
 convertTo12HourFormat(hour: number): string {
    const ampm = hour >= 12 ? 'PM' : 'AM';
    const displayHour = hour % 12 === 0 ? 12 : hour % 12; // Convert to 12-hour format
    return `${displayHour}:00 ${ampm}`;
  }
  receiveData(tHours: number) {
    if (tHours != 0) {
      this.sample = this.convertMinutesToHours(tHours);
    }
    else {
      this.sample = "00:00";
    }
  }

  changeCalendarView() {
    switch (this.selectedView) {
      case 'week':
        this.selectedDate = moment();
        this.getWeek();
        this.getAttendances(null, this.selectedTeamId);
        break;
      case 'month':
        this.selectedDate = moment();
        this.getMonth();
        this.getAttendances(null, this.selectedTeamId);
        break;
      default:
        break;
    }
  }

  getAllDropdownList() {
    this._attendancesServiceProxy.customGetTeamDetails()
      .subscribe((res) => {
        this.sortDropdown(res.teamIds);
      });
  }

  sortDropdown(records) {
    let removeRecord;
    this.reportFiltersInit.teamIds = records;
    for (let d of records) {
      if (d.userId == this._abpSessionService.userId) {
        removeRecord = d;
      }
    }
    if (removeRecord) {
      this.reportFiltersInit.teamIds = records.filter(x => x.userId != removeRecord.userId);
      this.reportFiltersInit.teamIds.unshift(removeRecord);
    }
  }

  currentDate() {
    this.selectedDate = moment();
    this.calculateWeekDates(this.selectedDate);
    this.getAttendances(null, this.selectedTeamId);
  }

  currentdate() {
    this.modelDate = new Date();
    this.weekStart = moment(this.modelDate).startOf('month').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('month');
    this.getAttendances(null, this.selectedTeamId);
  }

  getWeek() {
    this.modelDate = new Date();
    this.weekStart = moment(this.modelDate).startOf('week').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('week');
  }

  getMonth() {
    this.modelDate = new Date();
    this.weekStart = moment(this.modelDate).startOf('month').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('month');
    this.getAttendances(null, this.selectedTeamId);
  }

  calculateWeekDates(date: moment.Moment) {
    const sunday = date.clone().startOf('week').endOf('date');
    const saturday = date.clone().endOf('week');
    this.weekStart = sunday;
    this.weekEnd = saturday;
    this.getAttendances(null, this.selectedTeamId); // Optional: Fetch attendances for the new week
}


  previousWeek() {
    this.selectedDate.subtract(1, 'week');
    this.calculateWeekDates(this.selectedDate);
  }

  nextWeek() {
    this.selectedDate.add(1, 'week');
    this.calculateWeekDates(this.selectedDate);
  }

  prevMonth() {
    let prevMonth = new Date(this.modelDate);
    prevMonth.setMonth(prevMonth.getMonth() - 1);
    if (this.modelDate.getDate() !== prevMonth.getDate()) {
      prevMonth.setDate(0);
    }
    this.modelDate = new Date(prevMonth);
    this.weekStart = moment(this.modelDate).startOf('month').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('month');
    this.getAttendances(null, this.selectedTeamId);
  }

  onOpenCalendar(container) {
    container.monthSelectHandler = (event: any): void => {
      container._store.dispatch(container._actions.select(event.date));
    };
    container.setViewMode('month');
  }

  nextMonth() {
    let nextMonth = new Date(this.modelDate);
    nextMonth.setMonth(nextMonth.getMonth() + 1);
    if (this.modelDate.getDate() !== nextMonth.getDate()) {
      nextMonth.setDate(0);
    }
    this.modelDate = new Date(nextMonth);
    this.weekStart = moment(this.modelDate).startOf('month').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('month');
    this.getAttendances(null, this.selectedTeamId);
  }

  exportCsv(): void {
    this.exportService.exportListViewToCsv(this.primengTableHelper.records, 'Records');
  }

  time(date: DateTime): string {
    const jsDate = date.toJSDate();
    return jsDate.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });
  }

  calculateEarlyLateStatus(checkInTime: DateTime): { earlyBy: number, lateBy: number } {
    const shiftStart = DateTime.fromObject({ hour: 9, minute: 0 });
    const diffInMinutes = checkInTime.diff(shiftStart, 'minutes').minutes;
    let earlyBy = 0;
    let lateBy = 0;
    if (diffInMinutes < 0) {
      earlyBy = Math.abs(diffInMinutes);
    } else if (diffInMinutes > 0) {
      lateBy = diffInMinutes;
    }
    return { earlyBy, lateBy };
  }

  currentLoginUserInformations(): void {
    this.empName = this.appsession.user.userName;
    this.emailAddress = this.appsession.user.emailAddress;
  }

  getAttendances(event?: LazyLoadEvent, userId?: string) {
    if (userId != null || userId != undefined) {
      this.userId = userId;
    }
    this.primengTableHelper.showLoadingIndicator();
    this._attendancesServiceProxy.getAllCustomListView(
      this._dateTimeService.getStartOfDayForDate(this.weekStart.toDate()),
      this._dateTimeService.getEndOfDayForDate(this.weekEnd.toDate()),
      this._dateTimeService.getStartOfDayForDate(this.modelDate),
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
      this.userId == null ? this._abpSession.userId.toString() : this.userId,
      undefined,
      undefined,
      undefined,
    ).subscribe(result => {
      this.primengTableHelper.records = result.attendanceDetailsForList;
      this.footerDetails = result.footerDetails;
      console.log(this.primengTableHelper.records,"res");
      
      this.primengTableHelper.hideLoadingIndicator();
    });
  }

  convertMinutesToHours(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    const formattedHours = hours > 0 ? `${hours}Hr ` : '';
    const formattedMinutes = remainingMinutes > 0 ? `${remainingMinutes}Mins` : '';
    return `${formattedHours}${formattedMinutes}`.trim();
  }

  findLateInandEarlyIn(lateIn: DateTime): string | undefined {
    if (!lateIn) {
      return '--';
    }
    if (lateIn) {
      let start = new Date(lateIn.toString());
      let startUTC = new Date(start.toISOString());
      let endUTC = new Date(startUTC);
      endUTC.setUTCHours(9, 0, 0, 0);
      if (startUTC.getTime() === endUTC.getTime()) {
        return '';
      }
      return this.calculateTimeDifference(startUTC, endUTC);
    }
    return undefined;
  }

  findLateOutandEarlyOut(lateOut: DateTime): string | undefined {
    if (!lateOut) {
      return '--';
    }
    if (lateOut) {
      let start = new Date(lateOut.toString());
      let startUTC = new Date(start.toISOString());
      if (startUTC.getUTCHours() === 18 && startUTC.getUTCMinutes() === 0 && startUTC.getUTCSeconds() === 0) {
        return ' ';
      }
      let endUTC = new Date(startUTC);
      endUTC.setUTCHours(18, 0, 0, 0);
      if ((startUTC.getTime() < endUTC.getTime())) {
        return this.calculateTimeDifference(startUTC, endUTC);
      } else {
        return this.calculateTimeDifference(startUTC, endUTC);
      }

    }
    return undefined;
  }

  calculateTimeDifference(start: Date, end: Date): string {
    const diffInMilliseconds = end.getTime() - start.getTime();
    let differenceInMinutes = diffInMilliseconds / (1000 * 60);
    const isNegative = differenceInMinutes < 0;
    differenceInMinutes = Math.abs(differenceInMinutes);
    const hours = Math.floor(differenceInMinutes / 60);
    const minutes = Math.floor(differenceInMinutes % 60);
    const formattedDifference = `${hours > 0 ? hours + 'Hr ' : ''}${minutes > 0 ? minutes + 'Mins' : ''}`.trim();
    return isNegative ? `Late by ${formattedDifference}` : `Early by ${formattedDifference}`;
  }
  padZero(value: number): string {
    return value < 10 ? '0' + value : value.toString();
  }
  calculateCurrentTimePosition(): number {
    // Assuming shiftStart and shiftEnd are numbers representing hours (e.g., 9 for 9:00 AM, 18 for 6:00 PM)
    const actualStartHour = this.shiftStart; // e.g., 9:00 AM
    const endHour = this.shiftEnd +1;           // e.g., 6:00 PM (18)

    const currentTime = new Date();
    const currentDate = moment(currentTime); // Using moment to handle date comparisons

    // Check if the current date is within the current week
    if (currentDate.isBetween(this.weekStart, this.weekEnd, undefined, '[]')) {
        const currentHour = currentTime.getHours();
        const currentMinutes = currentTime.getMinutes();

        // Calculate how much time has passed since the shift start
        const timeSinceStart = (currentHour - actualStartHour) + (currentMinutes / 60);

        // Total working hours (from actualStartHour to endHour)
        const totalWorkingHours = endHour - actualStartHour;

        // Calculate the percentage of the current time within the total working hours
        const percentage = (timeSinceStart / totalWorkingHours) * 100;

        // Ensure the percentage is within the range of 0% to 100%
        return Math.min(Math.max(percentage, 0), 100);
    }

    // Return -1 to indicate the line should not be shown
    return -1;
}

}
