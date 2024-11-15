import { Input } from '@angular/core';
import { AppConsts } from '@shared/AppConsts';
import { Component, Injector, ViewChild } from '@angular/core';
import { AttendanceDetails, AttendancesServiceProxy, FooterDetails, } from '@shared/service-proxies/service-proxies';
import { AbpSessionService } from 'abp-ng2-module';
import { AppComponentBase } from '@shared/common/app-component-base';
import { CreateOrEditAttendanceModalComponent } from '../create-or-edit-attendance-modal.component';
import { ViewAttendanceModalComponent } from '../view-attendance-modal.component';
import { Table } from 'primeng/table';
import { Paginator } from 'primeng/paginator';
import { LazyLoadEvent } from 'primeng/api';
import { FileDownloadService } from '@shared/utils/file-download.service';
import { EntityTypeHistoryModalComponent } from '@app/shared/common/entityHistory/entity-type-history-modal.component';
import { filter as _filter } from 'lodash-es';
import { DateTime } from 'luxon';
import { DateTimeService } from '@app/shared/common/timing/date-time.service';
import { FormControl, FormGroup } from '@angular/forms';
import { SharedService } from '@shared/services/shared.service';
import * as moment from 'moment';
import { Dropdown } from 'primeng/dropdown';
import { ExportService } from '@shared/services/export-service';

@Component({
  selector: 'app-tabular-view',
  templateUrl: './tabular-view.component.html',
  styleUrls: ['./tabular-view.component.css']
})
export class TabularViewComponent extends AppComponentBase {
  [x: string]: any;
  selectedTeamId: string;
  @Input("item2") item;
  @Input() profileImageCssClass = '';
  @ViewChild('entityTypeHistoryModal', { static: true }) entityTypeHistoryModal: EntityTypeHistoryModalComponent;
  @ViewChild('createOrEditAttendanceModal', { static: true }) createOrEditAttendanceModal: CreateOrEditAttendanceModalComponent;
  @ViewChild('viewAttendanceModal', { static: true }) viewAttendanceModal: ViewAttendanceModalComponent;
  @ViewChild('dataTable', { static: true }) dataTable: Table;
  @ViewChild('paginator', { static: true }) paginator: Paginator;
  @ViewChild('dropdown') dropdown: Dropdown;
  advancedFiltersAreShown = false;
  profilePicture = AppConsts.appBaseUrl + '/assets/common/images/default-profile-picture.png';
  records: AttendanceDetails[];
  attendance: AttendanceDetails[] = [];
  filterText = '';
  maxCheckInFilter: DateTime;
  minCheckInFilter: DateTime;
  maxCheckOutFilter: DateTime;
  minCheckOutFilter: DateTime;
  maxTotalMinutesFilter: number;
  maxTotalMinutesFilterEmpty: number;
  minTotalMinutesFilter: number;
  minTotalMinutesFilterEmpty: number;
  employeeIdFilter = '';
  maxEventtimeFilter: DateTime;
  minEventtimeFilter: DateTime;
  ischeckinFilter = -1;
  maxDownloaddateFilter: DateTime;
  minDownloaddateFilter: DateTime;
  sourceTypeFilter = '';
  userNameFilter = '';
  isList: boolean = true;
  isTable: boolean = false;
  calendar: FormGroup;
  footerDetails: FooterDetails;
  weekEnd: moment.Moment;
  weekStart: moment.Moment;
  selectedDate: moment.Moment;
  selectedView: string = 'week';
  modelDate: any;
  selectedMember: number = null;
  empName: string;
  empFname: string;
  empSname: string;
  empId: string;
  emailAddress: string;
  reportFiltersInit: any = {
    "teamIds": []
  }
  constructor(
    injector: Injector,
    private _attendancesServiceProxy: AttendancesServiceProxy,
    private _fileDownloadService: FileDownloadService,
    private _dateTimeService: DateTimeService,
    private _abpSessionService: AbpSessionService,
    private _sharedService: SharedService,
    private exportService: ExportService,
    
  ) {
    super(injector);
    this.calendar = new FormGroup({
      calType: new FormControl('0'),
    });
  }

  ngOnInit(): void {
    this.currentDate();
    this.currentLoginUserInformations();
    this.getAllDropdownList();
    this.selectedTeamId = this._abpSessionService.userId.toString();
    if (this.item) {
      this._sharedService.checkinEventListener.subscribe((data) => {
        console.log(data);
        
        if (data != null) {
          this.getAttendances();
        }
      });
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

  exportCsv(): void {
    this.exportService.exportToCsv(this.primengTableHelper.records, 'Records');
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
  }
  calculateWeekDates(date: moment.Moment) {
    const sunday = date.clone().startOf('week').endOf('date');
    const saturday = date.clone().endOf('week');
    this.weekStart = sunday;
    this.weekEnd = saturday;
    this.dojOfWeekStartDate = this.weekStart.format('YYYY-MM-DD'); 
    this.getAttendances(null, this.selectedTeamId);
  }
  previousWeek() {
    this.selectedDate.subtract(1, 'week');
    this.calculateWeekDates(this.selectedDate);
    this.getAttendances(null, this.selectedTeamId);
  }
  nextWeek() {
    this.selectedDate.add(1, 'week');
    this.calculateWeekDates(this.selectedDate);
    this.getAttendances(null, this.selectedTeamId);
  }
  currentDate() {
    this.selectedDate = moment();
    this.calculateWeekDates(this.selectedDate);
    this.getAttendances(null, this.selectedTeamId);
  }

  prevMonth() {
    let prevMonth = new Date(this.modelDate);
    prevMonth.setMonth(prevMonth.getMonth() - 1);

    // Handle edge case where the previous month doesn't have the same day
    // (e.g., going from March 30th to February)
    if (this.modelDate.getDate() !== prevMonth.getDate()) {
      // Set the day to the last day of the current month
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

    // Handle edge case where the next month doesn't have the same day
    // (e.g., going from January 30th to February)
    if (this.modelDate.getDate() !== nextMonth.getDate()) {
      // Set the day to the last day of the current month
      nextMonth.setDate(0);
    }
    this.modelDate = new Date(nextMonth);
    this.weekStart = moment(this.modelDate).startOf('month').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('month');
    this.getAttendances(null, this.selectedTeamId);
  }

  currentdate() {
    this.modelDate = new Date();
    this.weekStart = moment(this.modelDate).startOf('month').endOf('date');
    this.weekEnd = moment(this.modelDate).endOf('month');
    this.getAttendances(null, this.selectedTeamId);
  }

  isListView() {
    this.isList = true;
    this.isTable = false;
  }
  isTableView() {
    this.isTable = true;
    this.isList = false;
  }

  time(date: DateTime) {
    return new Date(date.toString()).toLocaleTimeString();
  }

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

  currentLoginUserInformations(): void {
    this.empName = this.appSession.user.userName;
    this.empFname = this.appSession.user.name;
    this.empSname = this.appSession.user.surname;
    this.emailAddress = this.appSession.user.emailAddress;

  }

  getAttendances(event?: LazyLoadEvent, userId?: any) {
    this.primengTableHelper.showLoadingIndicator();
    this._attendancesServiceProxy.getAllCustom(
      this._dateTimeService.getStartOfDayForDateTab(this.weekStart.toDate()),
      this._dateTimeService.getEndOfDayForDateTab(this.weekEnd.toDate()),
      this._dateTimeService.getStartOfDayForDateTab(this.modelDate),
      this.filterText,
      this.maxCheckInFilter === undefined ? this.maxCheckInFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxCheckInFilter),
      this.minCheckInFilter === undefined ? this.minCheckInFilter : this._dateTimeService.getStartOfDayForDateTab(this.minCheckInFilter),
      this.maxCheckOutFilter === undefined ? this.maxCheckOutFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxCheckOutFilter),
      this.minCheckOutFilter === undefined ? this.minCheckOutFilter : this._dateTimeService.getStartOfDayForDateTab(this.minCheckOutFilter),
      this.maxTotalMinutesFilter == null ? this.maxTotalMinutesFilterEmpty : this.maxTotalMinutesFilter,
      this.minTotalMinutesFilter == null ? this.minTotalMinutesFilterEmpty : this.minTotalMinutesFilter,
      this.employeeIdFilter,
      this.maxEventtimeFilter === undefined ? this.maxEventtimeFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxEventtimeFilter),
      this.minEventtimeFilter === undefined ? this.minEventtimeFilter : this._dateTimeService.getStartOfDayForDateTab(this.minEventtimeFilter),
      this.ischeckinFilter,
      this.maxDownloaddateFilter === undefined ? this.maxDownloaddateFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxDownloaddateFilter),
      this.minDownloaddateFilter === undefined ? this.minDownloaddateFilter : this._dateTimeService.getStartOfDayForDateTab(this.minDownloaddateFilter),
      this.sourceTypeFilter,
      userId == null ? this._abpSessionService.userId.toString() : userId,
      this.primengTableHelper.getSorting(this.dataTable),
      this.primengTableHelper.getSkipCount(this.paginator, event),
      40
    ).subscribe(result => {
      this.primengTableHelper.records = result.attendanceDetails;
      console.log("result", this.primengTableHelper.records);
      this.footerDetails = result.footerDetails;
      this.primengTableHelper.hideLoadingIndicator();
    });
  }
  exportToExcel(): void {
    this._attendancesServiceProxy.getAttendancesToExcel(
      this.filterText,
      this.maxCheckInFilter === undefined ? this.maxCheckInFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxCheckInFilter),
      this.minCheckInFilter === undefined ? this.minCheckInFilter : this._dateTimeService.getStartOfDayForDateTab(this.minCheckInFilter),
      this.maxCheckOutFilter === undefined ? this.maxCheckOutFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxCheckOutFilter),
      this.minCheckOutFilter === undefined ? this.minCheckOutFilter : this._dateTimeService.getStartOfDayForDateTab(this.minCheckOutFilter),
      this.maxTotalMinutesFilter == null ? this.maxTotalMinutesFilterEmpty : this.maxTotalMinutesFilter,
      this.minTotalMinutesFilter == null ? this.minTotalMinutesFilterEmpty : this.minTotalMinutesFilter,
      this.employeeIdFilter,
      this.maxEventtimeFilter === undefined ? this.maxEventtimeFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxEventtimeFilter),
      this.minEventtimeFilter === undefined ? this.minEventtimeFilter : this._dateTimeService.getStartOfDayForDateTab(this.minEventtimeFilter),
      this.ischeckinFilter,
      this.maxDownloaddateFilter === undefined ? this.maxDownloaddateFilter : this._dateTimeService.getEndOfDayForDateTab(this.maxDownloaddateFilter),
      this.minDownloaddateFilter === undefined ? this.minDownloaddateFilter : this._dateTimeService.getStartOfDayForDateTab(this.minDownloaddateFilter),
      this.sourceTypeFilter,
      this.userNameFilter,
    )
      .subscribe(result => {
        this._fileDownloadService.downloadTempFile(result);
      });
  }

  resetFilters(): void {
    this.filterText = '';
    this.maxCheckInFilter = undefined;
    this.minCheckInFilter = undefined;
    this.maxCheckOutFilter = undefined;
    this.minCheckOutFilter = undefined;
    this.maxTotalMinutesFilter = this.maxTotalMinutesFilterEmpty;
    this.minTotalMinutesFilter = this.maxTotalMinutesFilterEmpty;
    this.employeeIdFilter = '';
    this.maxEventtimeFilter = undefined;
    this.minEventtimeFilter = undefined;
    this.ischeckinFilter = -1;
    this.maxDownloaddateFilter = undefined;
    this.minDownloaddateFilter = undefined;
    this.sourceTypeFilter = '';
    this.userNameFilter = '';
    this.getAttendances();
  }

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