import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';
import GoogleLogin from './GoogleLogin';

import logo from './logo.svg';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

interface IAppState {
    token: string;
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = { token: "" };
    }

    public render() {
        const callback = (value: any) => this.setState({ token: value });

        return (
            <div className="App">
                <header className="App-header">
                    <img src={logo} className="App-logo" alt="logo" />
                    <h1 className="App-title">Atomic Counter</h1>
                </header>
                <p className="App-intro">
                    {this.state.token}
                </p>
                <GoogleLogin tokenCallback={callback} />
            </div>
        );
    }
}

export default App;
