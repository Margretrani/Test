import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';
import {AttendancesComponent} from './attendances.component';
import { Canvas } from './canvas-view/canvas-view.component';


const routes: Routes = [
    {
        path: '',
        children: [{path: '', component: Canvas}],
        component: AttendancesComponent,
        pathMatch: 'full'
    },
    
    
];


@NgModule({
    imports: [RouterModule.forChild(routes)],
    exports: [RouterModule],
})
export class AttendanceRoutingModule {
}
