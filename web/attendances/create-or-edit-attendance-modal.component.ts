import { Component, ViewChild, Injector, Output, EventEmitter, OnInit, ElementRef} from '@angular/core';
import { ModalDirective } from 'ngx-bootstrap/modal';
import { finalize } from 'rxjs/operators';
import { AttendancesServiceProxy, CreateOrEditAttendanceDto } from '@shared/service-proxies/service-proxies';
import { AppComponentBase } from '@shared/common/app-component-base';
import { DateTime } from 'luxon';

             import { DateTimeService } from '@app/shared/common/timing/date-time.service';
import { AttendanceUserLookupTableModalComponent } from './attendance-user-lookup-table-modal.component';




@Component({
    selector: 'createOrEditAttendanceModal',
    templateUrl: './create-or-edit-attendance-modal.component.html'
})
export class CreateOrEditAttendanceModalComponent extends AppComponentBase implements OnInit{
   
    @ViewChild('createOrEditModal', { static: true }) modal: ModalDirective;
    @ViewChild('attendanceUserLookupTableModal', { static: true }) attendanceUserLookupTableModal: AttendanceUserLookupTableModalComponent;

    @Output() modalSave: EventEmitter<any> = new EventEmitter<any>();

    active = false;
    saving = false;
    

    attendance: CreateOrEditAttendanceDto = new CreateOrEditAttendanceDto();

    userName = '';



    constructor(
        injector: Injector,
        private _attendancesServiceProxy: AttendancesServiceProxy,
             private _dateTimeService: DateTimeService
    ) {
        super(injector);
    }
    
    show(attendanceId?: number): void {
    

        if (!attendanceId) {
            this.attendance = new CreateOrEditAttendanceDto();
            this.attendance.id = attendanceId;
            this.attendance.checkIn = this._dateTimeService.getStartOfDay();
            this.attendance.checkOut = this._dateTimeService.getStartOfDay();
            this.attendance.eventtime = this._dateTimeService.getStartOfDay();
            this.attendance.downloaddate = this._dateTimeService.getStartOfDay();
            this.userName = '';


            this.active = true;
            this.modal.show();
        } else {
            this._attendancesServiceProxy.getAttendanceForEdit(attendanceId).subscribe(result => {
                this.attendance = result.attendance;

                this.userName = result.userName;


                this.active = true;
                this.modal.show();
            });
        }
        
        
    }

    save(): void {
            this.saving = true;
            
			
			
            this._attendancesServiceProxy.createOrEdit(this.attendance)
             .pipe(finalize(() => { this.saving = false;}))
             .subscribe(() => {
                this.notify.info(this.l('SavedSuccessfully'));
                this.close();
                this.modalSave.emit(null);
             });
    }

    openSelectUserModal() {
        this.attendanceUserLookupTableModal.id = this.attendance.userId;
        this.attendanceUserLookupTableModal.displayName = this.userName;
        this.attendanceUserLookupTableModal.show();
    }






    setUserIdNull() {
        this.attendance.userId = null;
        this.userName = '';
    }


    getNewUserId() {
        this.attendance.userId = this.attendanceUserLookupTableModal.id;
        this.userName = this.attendanceUserLookupTableModal.displayName;
    }








    close(): void {
        this.active = false;
        this.modal.hide();
    }
    
     ngOnInit(): void {
        
     }    
}
