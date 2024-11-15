import {AppConsts} from '@shared/AppConsts';
import { Component, Injector, ViewEncapsulation, ViewChild } from '@angular/core';
import { ActivatedRoute , Router} from '@angular/router';
import { AttendancesServiceProxy, AttendanceDto  } from '@shared/service-proxies/service-proxies';
import { AbpSessionService, NotifyService } from 'abp-ng2-module';
import { AppComponentBase } from '@shared/common/app-component-base';
import { TokenAuthServiceProxy } from '@shared/service-proxies/service-proxies';
import { CreateOrEditAttendanceModalComponent } from './create-or-edit-attendance-modal.component';

import { ViewAttendanceModalComponent } from './view-attendance-modal.component';
import { appModuleAnimation } from '@shared/animations/routerTransition';
import { Table } from 'primeng/table';
import { Paginator } from 'primeng/paginator';
import { LazyLoadEvent } from 'primeng/api';
import { FileDownloadService } from '@shared/utils/file-download.service';
import { EntityTypeHistoryModalComponent } from '@app/shared/common/entityHistory/entity-type-history-modal.component';
import { filter as _filter } from 'lodash-es';
import { DateTime } from 'luxon';

             import { DateTimeService } from '@app/shared/common/timing/date-time.service';
import { UtcToLocalPipe } from '@app/shared/pipes/utc-to-local';


@Component({
    templateUrl: './attendances.component.html',
    encapsulation: ViewEncapsulation.None,
    animations: [appModuleAnimation()],
    styleUrls: ['./attendances.component.css']
})
export class AttendancesComponent extends AppComponentBase {
    
    
    @ViewChild('entityTypeHistoryModal', { static: true }) entityTypeHistoryModal: EntityTypeHistoryModalComponent;
    @ViewChild('createOrEditAttendanceModal', { static: true }) createOrEditAttendanceModal: CreateOrEditAttendanceModalComponent;
    @ViewChild('viewAttendanceModal', { static: true }) viewAttendanceModal: ViewAttendanceModalComponent;   
    
    @ViewChild('dataTable', { static: true }) dataTable: Table;
    @ViewChild('paginator', { static: true }) paginator: Paginator;
    

    advancedFiltersAreShown = false;
    filterText = '';
    maxCheckInFilter : DateTime;
		minCheckInFilter : DateTime;
    maxCheckOutFilter : DateTime;
		minCheckOutFilter : DateTime;
    maxTotalMinutesFilter : number;
		maxTotalMinutesFilterEmpty : number;
		minTotalMinutesFilter : number;
		minTotalMinutesFilterEmpty : number;
    employeeIdFilter = '';
    maxEventtimeFilter : DateTime;
		minEventtimeFilter : DateTime;
    ischeckinFilter = -1;
    maxDownloaddateFilter : DateTime;
		minDownloaddateFilter : DateTime;
    sourceTypeFilter = '';
        userNameFilter = '';


    _entityTypeFullName = 'Pulse.RubixProduct.Attendance';
    entityHistoryEnabled = false;


isCalendar: boolean=true;
isList:boolean=false;
isTable:boolean=false;
    constructor(
        injector: Injector,
        private _attendancesServiceProxy: AttendancesServiceProxy,
        private _notifyService: NotifyService,
        private _tokenAuth: TokenAuthServiceProxy,
        private _activatedRoute: ActivatedRoute,
        private _fileDownloadService: FileDownloadService,
        private _dateTimeService: DateTimeService,
        private _abpSessionService: AbpSessionService,
        private utcToLocal : UtcToLocalPipe
    ) {
        super(injector);
        
    }

    ngOnInit(): void {
        this.entityHistoryEnabled = this.setIsEntityHistoryEnabled();
    }
    isListView(){
        this.isList=true;
        this.isTable=false;
        this.isCalendar=false;
    }
    isTableView(){
        this.isTable=true;
        this.isList=false;
        this.isCalendar=false;
    }
    isCalendarView(){
        this.isTable=false;
        this.isList=false;
        this.isCalendar=true;
    }
    private setIsEntityHistoryEnabled(): boolean {
        let customSettings = (abp as any).custom;
        return this.isGrantedAny('Pages.Administration.AuditLogs') && customSettings.EntityHistory && customSettings.EntityHistory.isEnabled && _filter(customSettings.EntityHistory.enabledEntities, entityType => entityType === this._entityTypeFullName).length === 1;
    }
time(date:DateTime){
    return new Date(date.toString()).toLocaleTimeString();
}

    getAttendances(event?: LazyLoadEvent) {
        if (this.primengTableHelper.shouldResetPaging(event)) {
            this.paginator.changePage(0);
            if (this.primengTableHelper.records &&
                this.primengTableHelper.records.length > 0) {
                return;
            }
        }

        this.primengTableHelper.showLoadingIndicator();

        this._attendancesServiceProxy.getAllCustom(
            undefined,
            undefined,
            undefined,
            this.filterText,
            this.maxCheckInFilter === undefined ? this.maxCheckInFilter : this._dateTimeService.getEndOfDayForDate(this.maxCheckInFilter),
            this.minCheckInFilter === undefined ? this.minCheckInFilter : this._dateTimeService.getStartOfDayForDate(this.minCheckInFilter),
            this.maxCheckOutFilter === undefined ? this.maxCheckOutFilter : this._dateTimeService.getEndOfDayForDate(this.maxCheckOutFilter),
            this.minCheckOutFilter === undefined ? this.minCheckOutFilter : this._dateTimeService.getStartOfDayForDate(this.minCheckOutFilter),
            this.maxTotalMinutesFilter == null ? this.maxTotalMinutesFilterEmpty: this.maxTotalMinutesFilter,
            this.minTotalMinutesFilter == null ? this.minTotalMinutesFilterEmpty: this.minTotalMinutesFilter,
            this.employeeIdFilter,
            this.maxEventtimeFilter === undefined ? this.maxEventtimeFilter : this._dateTimeService.getEndOfDayForDate(this.maxEventtimeFilter),
            this.minEventtimeFilter === undefined ? this.minEventtimeFilter : this._dateTimeService.getStartOfDayForDate(this.minEventtimeFilter),
            this.ischeckinFilter,
            this.maxDownloaddateFilter === undefined ? this.maxDownloaddateFilter : this._dateTimeService.getEndOfDayForDate(this.maxDownloaddateFilter),
            this.minDownloaddateFilter === undefined ? this.minDownloaddateFilter : this._dateTimeService.getStartOfDayForDate(this.minDownloaddateFilter),
            this.sourceTypeFilter,
            this._abpSessionService.userId.toString(),
            this.primengTableHelper.getSorting(this.dataTable),
            this.primengTableHelper.getSkipCount(this.paginator, event),
            this.primengTableHelper.getMaxResultCount(this.paginator, event)
        ).subscribe(result => {
            // this.primengTableHelper.totalRecordsCount = result.totalCount;
            this.primengTableHelper.records = result.attendanceDetails;
            this.primengTableHelper.hideLoadingIndicator();
            // console.log("ht", result.items);
        });
    }

    reloadPage(): void {
        this.paginator.changePage(this.paginator.getPage());
    }

    getRecordFirstin(value:any){
        let dateForm = this.utcToLocal.transform(value);
        return dateForm;
    }

    createAttendance(): void {
        this.createOrEditAttendanceModal.show();        
    }


    showHistory(attendance: AttendanceDto): void {
        this.entityTypeHistoryModal.show({
            entityId: attendance.id.toString(),
            entityTypeFullName: this._entityTypeFullName,
            entityTypeDescription: ''
        });
    }

    deleteAttendance(attendance: AttendanceDto): void {
        this.message.confirm(
            '',
            this.l('AreYouSure'),
            (isConfirmed) => {
                if (isConfirmed) {
                    this._attendancesServiceProxy.delete(attendance.id)
                        .subscribe(() => {
                            this.reloadPage();
                            this.notify.success(this.l('SuccessfullyDeleted'));
                        });
                }
            }
        );
    }

    exportToExcel(): void {
        this._attendancesServiceProxy.getAttendancesToExcel(
        this.filterText,
            this.maxCheckInFilter === undefined ? this.maxCheckInFilter : this._dateTimeService.getEndOfDayForDate(this.maxCheckInFilter),
            this.minCheckInFilter === undefined ? this.minCheckInFilter : this._dateTimeService.getStartOfDayForDate(this.minCheckInFilter),
            this.maxCheckOutFilter === undefined ? this.maxCheckOutFilter : this._dateTimeService.getEndOfDayForDate(this.maxCheckOutFilter),
            this.minCheckOutFilter === undefined ? this.minCheckOutFilter : this._dateTimeService.getStartOfDayForDate(this.minCheckOutFilter),
            this.maxTotalMinutesFilter == null ? this.maxTotalMinutesFilterEmpty: this.maxTotalMinutesFilter,
            this.minTotalMinutesFilter == null ? this.minTotalMinutesFilterEmpty: this.minTotalMinutesFilter,
            this.employeeIdFilter,
            this.maxEventtimeFilter === undefined ? this.maxEventtimeFilter : this._dateTimeService.getEndOfDayForDate(this.maxEventtimeFilter),
            this.minEventtimeFilter === undefined ? this.minEventtimeFilter : this._dateTimeService.getStartOfDayForDate(this.minEventtimeFilter),
            this.ischeckinFilter,
            this.maxDownloaddateFilter === undefined ? this.maxDownloaddateFilter : this._dateTimeService.getEndOfDayForDate(this.maxDownloaddateFilter),
            this.minDownloaddateFilter === undefined ? this.minDownloaddateFilter : this._dateTimeService.getStartOfDayForDate(this.minDownloaddateFilter),
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
}
