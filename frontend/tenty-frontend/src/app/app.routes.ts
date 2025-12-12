import { Routes } from '@angular/router';
import { loginRedirctGuard, routeGuard } from './components/auth/route-guard';
import { LoginPage } from './components/pages/login-page/login-page';
import { Overlay } from './components/pages/overlay/overlay';
import { Home } from './components/pages/home/home';
import { CreateCampForm } from './components/forms/create-camp-form/create-camp-form';
import { MainCampViewComponent } from './components/pages/camp/main-camp-view/main-camp-view';

export const routes: Routes = [
    {path: 'login', component: LoginPage, canActivate: [loginRedirctGuard]
    },
    {path: '', component: Overlay, canActivate: [routeGuard],
        children: [
            {path: '', pathMatch: 'full', redirectTo: 'home'},
            {path: 'home', component: Home},
            {path: 'create-camp', component: CreateCampForm},
            {path: 'camp/:id', component: MainCampViewComponent },

        ]
    }
    
    
];
