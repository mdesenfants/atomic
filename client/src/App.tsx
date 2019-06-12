import * as React from 'react';
import './App.css';

import { AtomicCounterClient, counterNameIsValid } from './atomic-counter/build/dist/atomicCounter';

import { AppBar, Button, CssBaseline, Paper, Toolbar } from '@material-ui/core';
import Grid from '@material-ui/core/Grid';
import TextField from '@material-ui/core/TextField';
import Typography from '@material-ui/core/Typography';

import StripeLogin from './StripeLogin';

import * as lev from 'fast-levenshtein';

interface IAppState {
    client: AtomicCounterClient | null;
    count: number;
    timeoutHandle: NodeJS.Timeout | null;
    counterName: string;
    otherCounters: string[];
    disabled: boolean;
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = {
            client: null,
            count: 0,
            counterName: "",
            disabled: true,
            otherCounters: [],
            timeoutHandle: null,
        };
    }

    public componentDidMount() {
        setInterval(() => {
            if (!this.state.disabled && counterNameIsValid(this.state.counterName) || this.state.otherCounters.indexOf(this.state.counterName) > -1) {
                if (this.state.client) {
                    this.state.client.count(this.state.counterName).then(x => this.setState({ count: x }));
                }
            }
        }, 20000);
    }

    public render() {
        const callback = (value: any) => {
            if (!this.state.client) {
                const curr = new AtomicCounterClient(() => Promise.resolve(window.localStorage.getItem('stripe_token') || ''));
                curr.getCounters().then(x => {
                    this.setState({ client: curr, otherCounters: x })
                });
            }
        };

        const selectCounter = (input: string) => {
            return () => {
                this.setState({
                    counterName: input || this.state.counterName,
                    disabled: true,
                });

                if (this.state.client) {
                    this.state.client.count(input).then(c => {
                        this.setState({
                            count: c,
                            disabled: false
                        });
                    });
                }
            };
        };

        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);
        const counter = () => this.createCounter();

        const searchIsSet = this.state.counterName != null && this.state.counterName !== '';

        const compare = (a: string, b: string): number =>
            (searchIsSet && this.state.otherCounters.indexOf(this.state.counterName) < 0)
                ? lev.get(a, this.state.counterName, { useCollator: true }) - lev.get(b, this.state.counterName, { useCollator: true })
                : (a < b ? -1 : 1);


        const counterToLi = (input: string) =>
            <p key={input} >
                <a href="#" onClick={selectCounter(input)}>
                    {input}
                </a>
            </p>;

        const counters = this.state.otherCounters.sort(compare).map(counterToLi);

        return (
            <React.Fragment>
                <AppBar position="static" color="default">
                    <Toolbar>
                        <Grid container={true} justify="space-between">
                            <Grid item={true}>
                                <Typography variant="h6" color="inherit">
                                    Atomic Counter
                                </Typography>
                            </Grid>
                            <Grid item={true}>
                                <StripeLogin tokenCallback={callback} />
                            </Grid>
                        </Grid>
                    </Toolbar>
                </AppBar>
                <div className="container">
                    <CssBaseline />

                    <Grid container={true} spacing={24}>
                        <Grid item={true} xs={3}>
                            <Typography variant="h6">Counters</Typography>
                            <TextField
                                id="standard-with-placeholder"
                                placeholder="Search counters"
                                margin="normal"
                                value={this.state.counterName}
                                onChange={handle}
                            />
                            <Button
                                onClick={counter}
                                hidden={this.state.otherCounters.indexOf(this.state.counterName) !== -1 || !counterNameIsValid(this.state.counterName)}>
                                Create Counter
                            </Button>
                            {counters}
                        </Grid>
                        <Grid item={true} xs={9}>
                            {this.renderTools()}
                        </Grid>
                    </Grid>
                </div >
            </React.Fragment>
        );
    }

    private renderTools(): React.ReactNode {
        if (!counterNameIsValid(this.state.counterName)) {
            return <Typography variant="h6">Select or create a counter to continue.</Typography>;
        };

        if (this.state.disabled && this.state.otherCounters.indexOf(this.state.counterName) > -1) {
            return <Typography variant="h6">Loading...</Typography>;
        }

        if (counterNameIsValid(this.state.counterName) && this.state.disabled) {
            return <Typography variant="h6">Create this counter to continue.</Typography>;
        }

        const inc = () => this.increment();
        const reset = () => this.reset();
        const lpad = (input: number) => input.toLocaleString(undefined, { minimumIntegerDigits: 12 });

        return (
            <Paper>
                <p>
                    {lpad(this.state.count)}
                </p>
                <hr />
                <form>
                    <label>Cost per increment</label>
                    <input type="text" pattern="[0-9]+.?[0-9]?" />
                    <br />
                    <label>Effective date</label>
                    <input type="text" pattern="[0-9]+.?[0-9]?" />
                    <button>Submit change</button>
                </form>
                <hr />
                <div title="Other actions" id="actions">
                    <button onClick={inc}>Increment</button>
                    <button onClick={reset}>Reset</button>
                </div>
            </Paper>
        );
    }

    private async createCounter() {
        if (this.state.client) {
            await this.state.client.createCounter(this.state.counterName);
            this.setState({ otherCounters: await this.state.client.getCounters() })
        }
    }

    private handleCounterNameChange(event: React.ChangeEvent<HTMLInputElement>) {
        const cname = event.target.value.toString().toLowerCase();
        this.setState({
            counterName: cname,
            disabled: true,
        });

        if (this.state.client) {
            if (this.state.otherCounters.indexOf(cname) === -1) {
                return;
            }

            this.state.client.count(cname).then(c => {
                this.setState({
                    count: c,
                    disabled: false
                });
            });
        }
    }

    private async increment(): Promise<void> {
        if (this.state.client) {
            await this.state.client.increment(this.state.counterName);
        }
    }

    private async reset(): Promise<void> {
        if (this.state.client) {
            await this.state.client.reset(this.state.counterName);
        }
    }
}

export default App;
