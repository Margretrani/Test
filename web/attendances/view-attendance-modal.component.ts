import {AppConsts} from "@shared/AppConsts";
import { Component, ViewChild, Injector, Output, EventEmitter } from '@angular/core';
import { ModalDirective } from 'ngx-bootstrap/modal';
import { GetAttendanceForViewDto, AttendanceDto } from '@shared/service-proxies/service-proxies';
import { AppComponentBase } from '@shared/common/app-component-base';

@Component({
    selector: 'viewAttendanceModal',
    templateUrl: './view-attendance-modal.component.html'
})
export class ViewAttendanceModalComponent extends AppComponentBase {

    @ViewChild('createOrEditModal', { static: true }) modal: ModalDirective;
    @Output() modalSave: EventEmitter<any> = new EventEmitter<any>();

    active = false;
    saving = false;

    item: GetAttendanceForViewDto;


    constructor(
        injector: Injector
    ) {
        super(injector);
        this.item = new GetAttendanceForViewDto();
        this.item.attendance = new AttendanceDto();
    }

    show(item: GetAttendanceForViewDto): void {
        this.item = item;
        this.active = true;
        this.modal.show();
    }
    
    

    close(): void {
        this.active = false;
        this.modal.hide();
    }
}
