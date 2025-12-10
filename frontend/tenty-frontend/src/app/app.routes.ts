import { Routes } from '@angular/router';
import { loginRedirctGuard, routeGuard } from './components/auth/route-guard';
import { LoginPage } from './components/pages/login-page/login-page';
import { Overlay } from './components/pages/overlay/overlay';
import { Home } from './components/pages/home/home';
import { CreateCampForm } from './components/forms/create-camp-form/create-camp-form';
import { Camp } from './components/pages/camp/camp';

export const routes: Routes = [
    {path: 'login', component: LoginPage, //canDeactivate: [loginRedirctGuard]
    },
    {path: '', component: Overlay, //canActivate: [routeGuard],
        children: [
            {path: 'home', component: Home},
            {path: 'create-camp', component: CreateCampForm},
            {path: 'camp/:id', component: Camp }

        ]
    }
    
    
];
