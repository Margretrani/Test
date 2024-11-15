import {NgModule} from '@angular/core';
import {AppSharedModule} from '@app/shared/app-shared.module';
import {AdminSharedModule} from '@app/admin/shared/admin-shared.module';
import {AttendanceRoutingModule} from './attendance-routing.module';
import {AttendancesComponent} from './attendances.component';
import {CreateOrEditAttendanceModalComponent} from './create-or-edit-attendance-modal.component';
import {ViewAttendanceModalComponent} from './view-attendance-modal.component';
import {AttendanceUserLookupTableModalComponent} from './attendance-user-lookup-table-modal.component';
import { UtcToLocalPipe } from '@app/shared/pipes/utc-to-local';
import { ListViewComponent } from './list-view/list-view.component';
import { TabularViewComponent } from './tabular-view/tabular-view.component';
import { CalendarViewComponent } from './calendar-view/calendar-view.component';
import { FooterViewComponent } from './footer-view/footer-view.component';
import { ProgressBar } from './list-view/progress-bar';
import { Canvas } from './canvas-view/canvas-view.component';
    					


@NgModule({
    declarations: [
        AttendancesComponent,
        CreateOrEditAttendanceModalComponent,
        ViewAttendanceModalComponent,
        AttendanceUserLookupTableModalComponent,
        ListViewComponent,
        TabularViewComponent,
        CalendarViewComponent,
        FooterViewComponent,
        ProgressBar,
        Canvas,
    ],
    imports: [AppSharedModule, AttendanceRoutingModule , AdminSharedModule ],
    
})
export class AttendanceModule {
}
